using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.Application.Interfaces.Redis;
using Milvaion.Application.Utils.Constants;
using Milvaion.Infrastructure.BackgroundServices.Base;
using Milvasoft.Core.Abstractions;
using Milvasoft.Milvaion.Sdk.Models;
using Milvasoft.Milvaion.Sdk.Utils;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Collections.Concurrent;
using System.Text.Json;

namespace Milvaion.Infrastructure.BackgroundServices;

/// <summary>
/// Consumes worker registration and heartbeat messages from RabbitMQ.
/// Stores runtime state in Redis for high performance.
/// </summary>
public class WorkerAutoDiscoveryService(IRedisWorkerService redisWorkerService,
                                        IOptions<RabbitMQOptions> rabbitOptions,
                                        IOptions<WorkerAutoDiscoveryOptions> options,
                                        ILoggerFactory loggerFactory,
                                        IServiceProvider serviceProvider,
                                        IMemoryStatsRegistry memoryStatsRegistry = null) : MemoryTrackedBackgroundService(loggerFactory, options.Value, memoryStatsRegistry)
{
    private readonly IRedisWorkerService _redisWorkerService = redisWorkerService;
    private readonly IMilvaLogger _logger = loggerFactory.CreateMilvaLogger<WorkerAutoDiscoveryService>();
    private readonly RabbitMQOptions _rabbitOptions = rabbitOptions.Value;
    private readonly WorkerAutoDiscoveryOptions _options = options.Value;
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private IConnection _connection;
    private IChannel _registrationChannel;
    private IChannel _heartbeatChannel;

    // Heartbeat batch processing (prevent memory leak from 1.8M queued heartbeats)
    private readonly ConcurrentQueue<(WorkerHeartbeatMessage Heartbeat, ulong DeliveryTag)> _heartbeatBatch = new();
    private readonly SemaphoreSlim _batchLock = new(1, 1);
    private const int _maxBatchSize = 100; // Process 100 heartbeats at once
    private const int _maxQueueSize = 10000; // Drop heartbeats if queue grows too large

    /// <inheritdoc/>
    protected override string ServiceName => "WorkerAutoDiscovery";

    /// <summary>
    /// Executes the background service to listen for worker messages.
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    protected override async Task ExecuteWithMemoryTrackingAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.Warning("Worker auto discovery is disabled. Skipping startup.");

            return;
        }

        _logger.Information("Worker auto discovery is starting (Redis-based)...");

        // Start batch processor for heartbeats
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(100, stoppingToken); // Process every 100ms
                await ProcessHeartbeatBatchAsync(stoppingToken);
                TrackMemoryAfterIteration();
            }
        }, stoppingToken);

        try
        {
            await ConnectAndConsumeAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Worker auto discovery is shutting down");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Fatal error in worker auto discovery");
            throw;
        }
    }

    private async Task ConnectAndConsumeAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitOptions.Host,
            Port = _rabbitOptions.Port,
            UserName = _rabbitOptions.Username,
            Password = _rabbitOptions.Password,
            VirtualHost = _rabbitOptions.VirtualHost,
            AutomaticRecoveryEnabled = true
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _registrationChannel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
        _heartbeatChannel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        // Declare queues
        await _registrationChannel.QueueDeclareAsync(WorkerConstant.Queues.WorkerRegistration, true, false, false, null, cancellationToken: stoppingToken);
        await _heartbeatChannel.QueueDeclareAsync(WorkerConstant.Queues.WorkerHeartbeat, true, false, false, null, cancellationToken: stoppingToken);

        // Set QoS prefetch to limit in-flight messages and prevent queue buildup during Redis slowdowns
        await _registrationChannel.BasicQosAsync(0, 10, false, stoppingToken);
        await _heartbeatChannel.BasicQosAsync(0, 100, false, stoppingToken); // Higher prefetch for high-frequency heartbeats

        _logger.Information("Connected to RabbitMQ. Consuming worker registration and heartbeat messages");

        // Registration consumer
        var registrationConsumer = new AsyncEventingBasicConsumer(_registrationChannel);

        registrationConsumer.ReceivedAsync += async (model, ea) =>
        {
            await ProcessRegistrationAsync(ea, stoppingToken);

            TrackMemoryAfterIteration();
        };

        await _registrationChannel.BasicConsumeAsync(WorkerConstant.Queues.WorkerRegistration, false, registrationConsumer, stoppingToken);

        // Heartbeat consumer
        var heartbeatConsumer = new AsyncEventingBasicConsumer(_heartbeatChannel);

        heartbeatConsumer.ReceivedAsync += async (model, ea) =>
        {
            await ProcessHeartbeatAsync(ea, stoppingToken);

            TrackMemoryAfterIteration();
        };

        await _heartbeatChannel.BasicConsumeAsync(WorkerConstant.Queues.WorkerHeartbeat, false, heartbeatConsumer, stoppingToken);

        // Start zombie worker cleanup task (detect dead workers and cleanup their consumer counts)
        _ = Task.Run(async () => await CleanupZombieWorkersAsync(stoppingToken), stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ProcessRegistrationAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        try
        {
            // Skip processing if shutdown is requested
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.Debug("Skipping registration processing due to shutdown");
                return;
            }

            var registration = JsonSerializer.Deserialize<WorkerDiscoveryRequest>(ea.Body.Span, ConstantJsonOptions.PropNameCaseInsensitive);

            if (registration == null)
            {
                _logger.Debug("Failed to deserialize worker registration");

                await SafeNackAsync(_registrationChannel, ea.DeliveryTag, cancellationToken);

                return;
            }

            // Store in Redis (fast, in-memory)
            var success = await _redisWorkerService.RegisterWorkerAsync(registration, cancellationToken);

            if (success)
            {
                var existingWorker = await _redisWorkerService.GetWorkerAsync(registration.WorkerId, cancellationToken);
                var instanceCount = existingWorker?.Instances?.Count ?? 1;

                _logger.Information("Worker {WorkerId} (Instance: {InstanceId}) registered in Redis. Total instances: {Count}", registration.WorkerId, registration.InstanceId, instanceCount);
            }
            else
            {
                _logger.Error("Failed to register worker {WorkerId} in Redis", registration.WorkerId);
            }

            await SafeAckAsync(_registrationChannel, ea.DeliveryTag, false, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process worker registration");

            await SafeNackAsync(_registrationChannel, ea.DeliveryTag, cancellationToken);
        }
    }

    private async Task ProcessHeartbeatAsync(BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.Debug("Skipping heartbeat processing due to shutdown");
                return;
            }

            var heartbeat = JsonSerializer.Deserialize<WorkerHeartbeatMessage>(ea.Body.Span, ConstantJsonOptions.PropNameCaseInsensitive);

            if (heartbeat == null)
            {
                await SafeNackAsync(_heartbeatChannel, ea.DeliveryTag, cancellationToken);
                return;
            }

            // Graceful shutdown - immediate processing
            if (heartbeat.IsStopping)
            {
                _logger.Warning("Worker instance {InstanceId} is shutting down gracefully, performing immediate cleanup", heartbeat.InstanceId);

                await using var scope = _serviceProvider.CreateAsyncScope();
                var redisScheduler = scope.ServiceProvider.GetService<IRedisSchedulerService>();

                if (redisScheduler != null)
                {
                    try
                    {
                        var removed = await redisScheduler.RemoveAllRunningJobsForWorkerAsync(heartbeat.InstanceId, cancellationToken);
                        _logger.Information("Cleaned up {Count} running jobs for shutting down instance {InstanceId}", removed, heartbeat.InstanceId);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Failed to cleanup running jobs for {InstanceId}", heartbeat.InstanceId);
                    }
                }

                var success = await _redisWorkerService.RemoveWorkerInstanceAsync(heartbeat.WorkerId, heartbeat.InstanceId, cancellationToken);

                if (success)
                    _logger.Information("Worker instance {InstanceId} cleaned up successfully on graceful shutdown", heartbeat.InstanceId);

                await SafeAckAsync(_heartbeatChannel, ea.DeliveryTag, false, cancellationToken);
                return;
            }

            // Check queue size limit (backpressure)
            if (_heartbeatBatch.Count >= _maxQueueSize)
            {
                _logger.Warning("Heartbeat batch queue full ({Count}). Dropping heartbeat from {WorkerId}/{InstanceId}", _heartbeatBatch.Count, heartbeat.WorkerId, heartbeat.InstanceId);
                await SafeAckAsync(_heartbeatChannel, ea.DeliveryTag, false, cancellationToken);
                return;
            }

            // Add to batch queue with delivery tag (ACK will be done in batch)
            _heartbeatBatch.Enqueue((heartbeat, ea.DeliveryTag));

            _logger.Debug("Heartbeat queued for batch processing: {WorkerId}/{InstanceId} (Queue: {Count})", heartbeat.WorkerId, heartbeat.InstanceId, _heartbeatBatch.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process worker heartbeat");
            await SafeNackAsync(_heartbeatChannel, ea.DeliveryTag, cancellationToken);
        }
    }

    /// <summary>
    /// Process heartbeats in batch.
    /// </summary>
    private async Task ProcessHeartbeatBatchAsync(CancellationToken cancellationToken)
    {
        if (!await _batchLock.WaitAsync(0, cancellationToken))
            return;

        try
        {
            if (_heartbeatBatch.IsEmpty)
                return;

            var batch = new List<(string WorkerId, string InstanceId, int CurrentJobs, DateTime Timestamp)>();
            ulong maxDeliveryTag = 0;

            // Dequeue up to 100 heartbeats
            while (_heartbeatBatch.TryDequeue(out var item) && batch.Count < _maxBatchSize)
            {
                batch.Add((item.Heartbeat.WorkerId, item.Heartbeat.InstanceId, item.Heartbeat.CurrentJobs, item.Heartbeat.Timestamp));

                // Track highest delivery tag for bulk ACK
                if (item.DeliveryTag > maxDeliveryTag)
                    maxDeliveryTag = item.DeliveryTag;
            }

            if (batch.Count == 0)
                return;

            // Single Redis batch call (1 network roundtrip for 100 heartbeats)
            var successCount = await _redisWorkerService.BulkUpdateHeartbeatsAsync(batch, cancellationToken);

            // Bulk ACK: ACK the highest delivery tag with multiple=true
            // This ACKs all messages up to and including maxDeliveryTag in single call
            await SafeAckAsync(_heartbeatChannel, maxDeliveryTag, true, cancellationToken);

            _logger.Debug("Processed {SuccessCount}/{TotalCount} heartbeats in batch (bulk ACK up to {DeliveryTag})", successCount, batch.Count, maxDeliveryTag);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process heartbeat batch");
        }
        finally
        {
            _batchLock.Release();
        }
    }

    /// <summary>
    /// Safe ACK operation that checks channel state before acknowledgment.
    /// Prevents "Already closed" exceptions during shutdown.
    /// </summary>
    /// <param name="channel">The RabbitMQ channel.</param>
    /// <param name="deliveryTag">The delivery tag to acknowledge.</param>
    /// <param name="multiple">If true, ACKs all messages up to and including deliveryTag (bulk ACK).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    private async Task SafeAckAsync(IChannel channel, ulong deliveryTag, bool multiple, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested || channel == null || channel.IsClosed)
            {
                _logger.Debug("Skipping ACK: Channel closed or shutdown requested (DeliveryTag: {DeliveryTag})", deliveryTag);
                return;
            }

            await channel.BasicAckAsync(deliveryTag, multiple, cancellationToken);
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
    private async Task SafeNackAsync(IChannel channel, ulong deliveryTag, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested || channel == null || channel.IsClosed)
            {
                _logger.Debug("Skipping NACK: Channel closed or shutdown requested (DeliveryTag: {DeliveryTag})", deliveryTag);
                return;
            }

            await channel.BasicNackAsync(deliveryTag, false, false, cancellationToken);
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
        _logger.Information("Worker auto discovery is stopping...");

        // Process remaining heartbeats before shutdown
        try
        {
            await ProcessHeartbeatBatchAsync(CancellationToken.None);
            _logger.Information("Processed remaining {Count} heartbeats during shutdown", _heartbeatBatch.Count);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Failed to process remaining heartbeats during shutdown");
        }

        // Dispose batch lock
        _batchLock?.Dispose();

        try
        {
            if (_registrationChannel != null && !_registrationChannel.IsClosed)
            {
                await _registrationChannel.CloseAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error closing registration channel");
        }
        finally
        {
            _registrationChannel?.Dispose();
        }

        try
        {
            if (_heartbeatChannel != null && !_heartbeatChannel.IsClosed)
            {
                await _heartbeatChannel.CloseAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Error closing heartbeat channel");
        }
        finally
        {
            _heartbeatChannel?.Dispose();
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
            _logger.Warning(ex, "Error closing connection");
        }
        finally
        {
            _connection?.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }

    /// <summary>
    /// Background task that detects dead worker instances (no heartbeat) and cleans up their consumer counts.
    /// Runs every 30 seconds to detect workers that haven't sent heartbeat for 60+ seconds.
    /// </summary>
    private async Task CleanupZombieWorkersAsync(CancellationToken stoppingToken)
    {
        _logger.Information("Zombie worker cleanup task started (checks every 30s, timeout: 60s)");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

                    // Get all workers from Redis
                    var workers = await _redisWorkerService.GetAllWorkersAsync(stoppingToken);

                    foreach (var worker in workers)
                    {
                        if (worker.Instances == null || worker.Instances.Count == 0)
                            continue;

                        var now = DateTime.UtcNow;
                        var deadInstances = worker.Instances.Where(i => i.LastHeartbeat != default && (now - i.LastHeartbeat).TotalSeconds > 60).ToList();

                        foreach (var deadInstance in deadInstances)
                        {
                            _logger.Warning("Detected dead worker instance: {InstanceId} (last heartbeat: {LastHeartbeat}, {Seconds}s ago)", deadInstance.InstanceId, deadInstance.LastHeartbeat, (now - deadInstance.LastHeartbeat).TotalSeconds);

                            // Cleanup consumer counts and instance metadata
                            var success = await _redisWorkerService.RemoveWorkerInstanceAsync(worker.WorkerId, deadInstance.InstanceId, stoppingToken);

                            if (success)
                                _logger.Information("Cleaned up dead worker instance: {InstanceId}", deadInstance.InstanceId);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error during zombie worker cleanup");
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Zombie worker cleanup task stopped");
        }
    }
}
