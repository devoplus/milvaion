using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvasoft.Core.Abstractions;
using Milvasoft.Milvaion.Sdk.Utils;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Milvasoft.Milvaion.Sdk.Worker.Persistence;

/// <summary>
/// Validates RabbitMQ and Redis connectivity at startup.
/// If connections cannot be established within the configured timeout, throws to stop the host (fail-fast).
/// </summary>
internal class ConnectionStartupValidator(IOptions<WorkerOptions> options,
                                          ILoggerFactory loggerFactory,
                                          IConnectionMultiplexer redis = null) : IHostedService
{
    private readonly WorkerOptions _options = options.Value;
    private readonly IMilvaLogger _logger = loggerFactory.CreateMilvaLogger<ConnectionStartupValidator>();
    private readonly IConnectionMultiplexer _redis = redis;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var timeoutSeconds = _options.StartupConnectionTimeoutSeconds;

        if (timeoutSeconds <= 0)
        {
            _logger.Information("Startup connection validation is disabled");
            return;
        }

        _logger.Information("Validating startup connections (timeout: {Timeout}s)...", timeoutSeconds);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        await ValidateRabbitMQAsync(timeoutCts.Token);
        await ValidateRedisAsync(timeoutCts.Token);

        _logger.Information("All startup connections validated successfully");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task ValidateRabbitMQAsync(CancellationToken cancellationToken)
    {
        var rabbitMQ = _options.RabbitMQ;
        var baseDelay = 2.0;
        var maxDelay = 10.0;
        var jitterRandom = new Random();

        var factory = new ConnectionFactory
        {
            HostName = rabbitMQ.Host,
            Port = rabbitMQ.Port,
            UserName = rabbitMQ.Username,
            Password = rabbitMQ.Password,
            VirtualHost = rabbitMQ.VirtualHost,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };

        var attempt = 0;

        while (true)
        {
            try
            {
                var connection = await factory.CreateConnectionAsync(cancellationToken);

                await connection.CloseAsync(cancellationToken);

                connection.Dispose();

                _logger.Information("RabbitMQ connection validated: {Host}:{Port}", rabbitMQ.Host, rabbitMQ.Port);

                return;
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to connect to RabbitMQ at {rabbitMQ.Host}:{rabbitMQ.Port} within {_options.StartupConnectionTimeoutSeconds}s after {attempt} attempt(s). Worker cannot start.");
            }
            catch (Exception ex)
            {
                attempt++;

                // Exponential backoff with jitter for startup retries
                var delay = Math.Min(baseDelay * Math.Pow(2, attempt - 1), maxDelay);

                var jitter = delay * 0.2 * (2 * jitterRandom.NextDouble() - 1);

                delay = Math.Max(1, delay + jitter);

                _logger.Warning("RabbitMQ not ready ({Host}:{Port}): {Error}. Retry #{Attempt} in {Delay:F1}s...", rabbitMQ.Host, rabbitMQ.Port, ex.Message, attempt, delay);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw new InvalidOperationException($"Failed to connect to RabbitMQ at {rabbitMQ.Host}:{rabbitMQ.Port} within {_options.StartupConnectionTimeoutSeconds}s after {attempt} attempt(s). Worker cannot start.");
                }
            }
        }
    }

    private async Task ValidateRedisAsync(CancellationToken cancellationToken)
    {
        if (_redis == null)
        {
            _logger.Debug("Redis not configured, skipping validation");
            return;
        }

        var baseDelay = 2.0;
        var maxDelay = 10.0;
        var jitterRandom = new Random();
        var attempt = 0;

        while (true)
        {
            try
            {
                if (_redis.IsConnected)
                {
                    _logger.Information("Redis connection validated");
                    return;
                }

                throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is not connected");
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException($"Failed to connect to Redis within {_options.StartupConnectionTimeoutSeconds}s after {attempt} attempt(s). Worker cannot start.");
            }
            catch (Exception ex)
            {
                attempt++;

                var delay = Math.Min(baseDelay * Math.Pow(2, attempt - 1), maxDelay);

                var jitter = delay * 0.2 * (2 * jitterRandom.NextDouble() - 1);

                delay = Math.Max(1, delay + jitter);

                _logger.Warning("Redis not ready: {Error}. Retry #{Attempt} in {Delay:F1}s...", ex.Message, attempt, delay);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw new InvalidOperationException($"Failed to connect to Redis within {_options.StartupConnectionTimeoutSeconds}s after {attempt} attempt(s). Worker cannot start.");
                }
            }
        }
    }
}
