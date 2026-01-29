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

            await SafeAckAsync(_registrationChannel, ea.DeliveryTag, cancellationToken);
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
            // Skip processing if shutdown is requested
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

            _logger.Debug("Received heartbeat: WorkerId={WorkerId}, InstanceId={InstanceId}, CurrentJobs={CurrentJobs}, IsStopping={IsStopping}", heartbeat.WorkerId, heartbeat.InstanceId, heartbeat.CurrentJobs, heartbeat.IsStopping);

            // If worker is shutting down gracefully, immediately cleanup
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

                // Cleanup consumer counts and instance metadata
                var success = await _redisWorkerService.RemoveWorkerInstanceAsync(heartbeat.WorkerId, heartbeat.InstanceId, cancellationToken);

                if (success)
                    _logger.Information("Worker instance {InstanceId} cleaned up successfully on graceful shutdown", heartbeat.InstanceId);

                await SafeAckAsync(_heartbeatChannel, ea.DeliveryTag, cancellationToken);
                return;
            }

            // Update in Redis (fast, in-memory)
            var updateSuccess = await _redisWorkerService.UpdateHeartbeatAsync(heartbeat.WorkerId, heartbeat.InstanceId, heartbeat.CurrentJobs, cancellationToken);

            if (!updateSuccess)
            {
                _logger.Warning("Heartbeat for unknown worker {WorkerId} instance {InstanceId}", heartbeat.WorkerId, heartbeat.InstanceId);
            }
            else
            {
                _logger.Debug("Heartbeat processed successfully for {WorkerId}/{InstanceId}", heartbeat.WorkerId, heartbeat.InstanceId);
            }

            await SafeAckAsync(_heartbeatChannel, ea.DeliveryTag, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to process worker heartbeat");

            await SafeNackAsync(_heartbeatChannel, ea.DeliveryTag, cancellationToken);
        }
    }

    /// <summary>
    /// Safe ACK operation that checks channel state before acknowledgment.
    /// Prevents "Already closed" exceptions during shutdown.
    /// </summary>
    private async Task SafeAckAsync(IChannel channel, ulong deliveryTag, CancellationToken cancellationToken)
    {
        try
        {
            if (cancellationToken.IsCancellationRequested || channel == null || channel.IsClosed)
            {
                _logger.Debug("Skipping ACK: Channel closed or shutdown requested (DeliveryTag: {DeliveryTag})", deliveryTag);
                return;
            }

            await channel.BasicAckAsync(deliveryTag, false, cancellationToken);
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
                        var deadInstances = worker.Instances
                            .Where(i => i.LastHeartbeat != default && (now - i.LastHeartbeat).TotalSeconds > 60)
                            .ToList();

                        foreach (var deadInstance in deadInstances)
                        {
                            _logger.Warning("Detected dead worker instance: {InstanceId} (last heartbeat: {LastHeartbeat}, {Seconds}s ago)",
                                deadInstance.InstanceId,
                                deadInstance.LastHeartbeat,
                                (now - deadInstance.LastHeartbeat).TotalSeconds);

                            // Cleanup consumer counts and instance metadata
                            var success = await _redisWorkerService.RemoveWorkerInstanceAsync(
                                worker.WorkerId,
                                deadInstance.InstanceId,
                                stoppingToken);

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
