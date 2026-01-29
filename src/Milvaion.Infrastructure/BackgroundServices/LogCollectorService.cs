using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.Application.Interfaces;
using Milvaion.Application.Utils.Constants;
using Milvaion.Infrastructure.BackgroundServices.Base;
using Milvaion.Infrastructure.Persistence.Context;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Helpers;
using Milvasoft.Milvaion.Sdk.Utils;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Milvaion.Infrastructure.BackgroundServices;

/// <summary>
/// Consumes worker logs from RabbitMQ and appends them to JobOccurrence.Logs.
/// </summary>
public class LogCollectorService(IServiceProvider serviceProvider,
                                 IOptions<RabbitMQOptions> rabbitOptions,
                                 IOptions<LogCollectorOptions> logCollectorOptions,
                                 ILoggerFactory loggerFactory,
                                 IMemoryStatsRegistry memoryStatsRegistry = null) : MemoryTrackedBackgroundService(loggerFactory, logCollectorOptions.Value, memoryStatsRegistry)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IMilvaLogger _logger = loggerFactory.CreateMilvaLogger<LogCollectorService>();
    private readonly RabbitMQOptions _rabbitOptions = rabbitOptions.Value;
    private readonly LogCollectorOptions _options = logCollectorOptions.Value;
    private IConnection _connection;
    private IChannel _channel;

    // Batch processing
    private readonly ConcurrentQueue<WorkerLogMessage> _logBatch = new();
    private readonly SemaphoreSlim _batchLock = new(1, 1);

    /// <inheritdoc/>
    protected override string ServiceName => "LogCollector";

    /// <inheritdoc />
    protected override async Task ExecuteWithMemoryTrackingAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.Warning("Log collection is disabled. Skipping startup.");

            return;
        }

        _logger.Information("Log collection starting...");

        // Start batch processor task
        var batchTask = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_options.BatchIntervalMs, stoppingToken);

                await ProcessBatchAsync(stoppingToken);

                TrackMemoryAfterIteration();
            }
        }, stoppingToken);

        var retryCount = 0;
        const int maxRetries = 10;
        const int retryDelaySeconds = 5;

        while (!stoppingToken.IsCancellationRequested && retryCount < maxRetries)
        {
            try
            {
                await ConnectAndConsumeAsync(stoppingToken);

                // If we reach here, connection was successful
                retryCount = 0;
            }
            catch (OperationCanceledException)
            {
                _logger.Information("Log collection is shutting down");

                break;
            }
            catch (Exception ex)
            {
                retryCount++;

                _logger.Error(ex, "LogCollectorService connection failed (attempt {Retry}/{MaxRetries})", retryCount, maxRetries);

                if (retryCount >= maxRetries)
                {
                    _logger.Fatal("LogCollectorService failed to connect after {MaxRetries} attempts. Service will be disabled until application restart.", maxRetries);

                    break;
                }

                await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds * retryCount), stoppingToken);
            }
        }
    }

    private async Task ConnectAndConsumeAsync(CancellationToken stoppingToken)
    {
        // Setup RabbitMQ connection
        var factory = new ConnectionFactory
        {
            HostName = _rabbitOptions.Host,
            Port = _rabbitOptions.Port,
            UserName = _rabbitOptions.Username,
            Password = _rabbitOptions.Password,
            VirtualHost = _rabbitOptions.VirtualHost,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);

        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Declare queue (idempotent)
        await _channel.QueueDeclareAsync(queue: WorkerConstant.Queues.WorkerLogs,
                                         durable: true,
                                         exclusive: false,
                                         autoDelete: false,
                                         arguments: null,
                                         cancellationToken: stoppingToken);

        // Set prefetch count
        await _channel.BasicQosAsync(0, 10, false, stoppingToken);

        _logger.Information("Connected to RabbitMQ. Queue: {Queue}", WorkerConstant.Queues.WorkerLogs);

        // Setup consumer
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (model, ea) =>
        {
            try
            {
                await ProcessLogMessageAsync(ea, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Unhandled exception in log consumer");
            }
        };

        await _channel.BasicConsumeAsync(queue: WorkerConstant.Queues.WorkerLogs,
                                         autoAck: false,
                                         consumer: consumer,
                                         cancellationToken: stoppingToken);

        _logger.Information("LogCollectorService is now consuming messages...");

        // Keep running until cancellation
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessLogMessageAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        try
        {
            // Try to parse as batch message first (new format)
            var batchMessage = JsonSerializer.Deserialize<WorkerLogBatchMessage>(ea.Body.Span, ConstantJsonOptions.PropNameCaseInsensitive);

            if (batchMessage != null && batchMessage.Count > 0)
            {
                // Batch message - add all logs
                foreach (var log in batchMessage.Logs)
                {
                    _logBatch.Enqueue(log);
                }

                _logger.Debug("Received batch message with {Count} logs", batchMessage.Count);

                // Trigger immediate batch if queue is full
                if (_logBatch.Count >= _options.BatchSize)
                {
                    await ProcessBatchAsync(cancellationToken);
                }

                // ACK the message (safe operation)
                await SafeAckAsync(ea.DeliveryTag, cancellationToken);
                return;
            }

            // Fallback: Try single message format (backward compatibility)
            var singleMessage = JsonSerializer.Deserialize<WorkerLogMessage>(ea.Body.Span, ConstantJsonOptions.PropNameCaseInsensitive);

            if (singleMessage == null)
            {
                _logger.Debug("Failed to deserialize log message");
                await SafeNackAsync(ea.DeliveryTag, cancellationToken);
                return;
            }

            // Add to batch queue (NO DB operation here!)
            _logBatch.Enqueue(singleMessage);

            // Trigger immediate batch if queue is full
            if (_logBatch.Count >= _options.BatchSize)
            {
                await ProcessBatchAsync(cancellationToken);
            }

            // ACK the message (safe operation)
            await SafeAckAsync(ea.DeliveryTag, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process log message");
            await SafeNackAsync(ea.DeliveryTag, cancellationToken);
        }
    }

    /// <summary>
    /// Process batch of logs - single DB transaction for all logs.
    /// Uses optimistic concurrency control to prevent lost updates when multiple instances run concurrently.
    /// </summary>
    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        // If batch processing is already in progress, skip
        if (!await _batchLock.WaitAsync(0, cancellationToken))
            return;

        try
        {
            if (_logBatch.IsEmpty)
                return;

            var batch = new List<WorkerLogMessage>();

            // Dequeue all pending logs
            while (_logBatch.TryDequeue(out var message))
                batch.Add(message);

            if (batch.Count == 0)
                return;

            // Retry logic for optimistic concurrency conflicts
            const int maxRetries = 3;
            var retryCount = 0;
            var retryDelay = TimeSpan.FromMilliseconds(50); // Start with 50ms

            while (retryCount < maxRetries)
            {
                try
                {
                    await using var scope = _serviceProvider.CreateAsyncScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<MilvaionDbContext>();

                    // Group by CorrelationId
                    var logsByCorrelation = batch.GroupBy(m => m.CorrelationId).ToList();

                    var logsToInsert = batch.Select(l => new JobOccurrenceLog
                    {
                        Id = Guid.CreateVersion7(),
                        OccurrenceId = l.CorrelationId,
                        Level = l.Log.Level,
                        Category = l.Log.Category,
                        ExceptionType = l.Log.ExceptionType,
                        Message = l.Log.Message,
                        Data = l.Log.Data,
                        Timestamp = l.Log.Timestamp,
                    });

                    if (!logsToInsert.IsNullOrEmpty())
                        await dbContext.BulkInsertAsync(logsToInsert, cancellationToken: cancellationToken);

                    #region Send Socket Events

                    // Trigger SignalR events after DB update
                    var eventPublisher = scope.ServiceProvider.GetService<IJobOccurrenceEventPublisher>();

                    if (eventPublisher != null)
                    {
                        //  Collect events first, then publish in batch
                        var publishTasks = new List<Task>(batch.Count);

                        foreach (var logToInsert in batch)
                            publishTasks.Add(eventPublisher.PublishLogAddedAsync(logToInsert.CorrelationId, logToInsert.Log, cancellationToken));

                        // Wait for all events to complete (with timeout)
                        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                        var completedTask = await Task.WhenAny(Task.WhenAll(publishTasks), timeoutTask);

                        if (completedTask == timeoutTask)
                            _logger.Warning("SignalR event publishing timed out after 5 seconds for {Count} events", publishTasks.Count);
                    }

                    #endregion

                    _logger.Debug("Processed {Count} logs in batch (RetryCount: {RetryCount})", batch.Count, retryCount);

                    // SUCCESS - Exit retry loop
                    break;
                }
                catch (DbUpdateConcurrencyException concurrencyEx)
                {
                    retryCount++;

                    if (retryCount >= maxRetries)
                    {
                        _logger.Error(concurrencyEx, "Concurrency conflict after {MaxRetries} retries. Logs will be retried in next batch.", maxRetries);

                        // Re-queue failed logs for next batch
                        foreach (var log in batch)
                            _logBatch.Enqueue(log);

                        break;
                    }

                    _logger.Warning(concurrencyEx, "Concurrency conflict detected in log batch processing (Retry {RetryCount}/{MaxRetries}). Retrying after {Delay}ms...", retryCount, maxRetries, retryDelay.TotalMilliseconds);

                    // Exponential backoff: 50ms, 100ms, 200ms
                    await Task.Delay(retryDelay, cancellationToken);
                    retryDelay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * 2);
                }
                catch (Npgsql.PostgresException pgEx) when (pgEx.SqlState == "40P01") // Deadlock
                {
                    _logger.Warning(pgEx, "Deadlock detected in log batch processing. Logs will be retried in next batch.");

                    // Re-queue logs for next batch
                    foreach (var log in batch)
                        _logBatch.Enqueue(log);

                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Failed to process log batch");

                    // Re-queue logs for next batch
                    foreach (var log in batch)
                        _logBatch.Enqueue(log);

                    break;
                }
            }
        }
        finally
        {
            // Release the batch lock
            _batchLock.Release();
        }
    }

    /// <summary>
    /// Safe ACK operation that checks channel state before acknowledgment.
    /// Prevents "Already closed" exceptions during shutdown.
    /// </summary>
    private async Task SafeAckAsync(ulong deliveryTag, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested || _channel == null || _channel.IsClosed)
            {
                _logger.Debug("Skipping ACK: Channel closed or shutdown requested (DeliveryTag: {DeliveryTag})", deliveryTag);
                return;
            }

            await _channel.BasicAckAsync(deliveryTag, false, cancellationToken);
        }
        catch (RabbitMQ.Client.Exceptions.AlreadyClosedException)
        {
            _logger.Debug("Channel already closed during ACK (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to ACK message (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
    }

    /// <summary>
    /// Safe NACK operation that checks channel state before negative acknowledgment.
    /// Prevents "Already closed" exceptions during shutdown.
    /// </summary>
    private async Task SafeNackAsync(ulong deliveryTag, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested || _channel == null || _channel.IsClosed)
            {
                _logger.Debug("Skipping NACK: Channel closed or shutdown requested (DeliveryTag: {DeliveryTag})", deliveryTag);
                return;
            }

            await _channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
        }
        catch (RabbitMQ.Client.Exceptions.AlreadyClosedException)
        {
            _logger.Debug("Channel already closed during NACK (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to NACK message (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
    }

    /// <summary>
    /// Stops the background service and cleans up resources.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.Information("LogCollectorService stopping...");

        try
        {
            // Process remaining logs before shutdown
            await ProcessBatchAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to process remaining logs during shutdown");
        }

        // Dispose semaphore
        _batchLock?.Dispose();

        try
        {
            if (_channel != null && !_channel.IsClosed)
            {
                await _channel.CloseAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error closing RabbitMQ channel");
        }
        finally
        {
            _channel?.Dispose();
        }

        try
        {
            if (_connection != null && _connection.IsOpen)
            {
                await _connection.CloseAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error closing RabbitMQ connection");
        }
        finally
        {
            _connection?.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}
