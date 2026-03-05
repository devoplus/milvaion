using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Milvasoft.Milvaion.Sdk.Worker.Persistence;

/// <summary>
/// Validates RabbitMQ and Redis connectivity at startup.
/// If connections cannot be established within the configured timeout, throws to stop the host (fail-fast).
/// </summary>
internal class ConnectionStartupValidator : IHostedService
{
    private readonly WorkerOptions _options;
    private readonly ILogger<ConnectionStartupValidator> _logger;
    private readonly IConnectionMultiplexer _redis;

    public ConnectionStartupValidator(IOptions<WorkerOptions> options,
                                      ILogger<ConnectionStartupValidator> logger,
                                      IConnectionMultiplexer redis = null)
    {
        _options = options.Value;
        _logger = logger;
        _redis = redis;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var timeoutSeconds = _options.StartupConnectionTimeoutSeconds;

        if (timeoutSeconds <= 0)
        {
            _logger.LogInformation("Startup connection validation is disabled");
            return;
        }

        _logger.LogInformation("Validating startup connections (timeout: {Timeout}s)...", timeoutSeconds);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        await ValidateRabbitMQAsync(timeoutCts.Token);
        await ValidateRedisAsync(timeoutCts.Token);

        _logger.LogInformation("All startup connections validated successfully");
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

                _logger.LogInformation("RabbitMQ connection validated: {Host}:{Port}", rabbitMQ.Host, rabbitMQ.Port);
                return;
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"Failed to connect to RabbitMQ at {rabbitMQ.Host}:{rabbitMQ.Port} within {_options.StartupConnectionTimeoutSeconds}s after {attempt} attempt(s). Worker cannot start.");
            }
            catch (Exception ex)
            {
                attempt++;

                // Exponential backoff with jitter for startup retries
                var delay = Math.Min(baseDelay * Math.Pow(2, attempt - 1), maxDelay);
                var jitter = delay * 0.2 * (2 * jitterRandom.NextDouble() - 1);
                delay = Math.Max(1, delay + jitter);

                _logger.LogWarning("RabbitMQ not ready ({Host}:{Port}): {Error}. Retry #{Attempt} in {Delay:F1}s...",
                    rabbitMQ.Host, rabbitMQ.Port, ex.Message, attempt, delay);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw new InvalidOperationException(
                        $"Failed to connect to RabbitMQ at {rabbitMQ.Host}:{rabbitMQ.Port} within {_options.StartupConnectionTimeoutSeconds}s after {attempt} attempt(s). Worker cannot start.");
                }
            }
        }
    }

    private async Task ValidateRedisAsync(CancellationToken cancellationToken)
    {
        if (_redis == null)
        {
            _logger.LogDebug("Redis not configured, skipping validation");
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
                    _logger.LogInformation("Redis connection validated");
                    return;
                }

                throw new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Redis is not connected");
            }
            catch (OperationCanceledException)
            {
                throw new InvalidOperationException(
                    $"Failed to connect to Redis within {_options.StartupConnectionTimeoutSeconds}s after {attempt} attempt(s). Worker cannot start.");
            }
            catch (Exception ex)
            {
                attempt++;

                var delay = Math.Min(baseDelay * Math.Pow(2, attempt - 1), maxDelay);
                var jitter = delay * 0.2 * (2 * jitterRandom.NextDouble() - 1);
                delay = Math.Max(1, delay + jitter);

                _logger.LogWarning("Redis not ready: {Error}. Retry #{Attempt} in {Delay:F1}s...",
                    ex.Message, attempt, delay);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delay), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw new InvalidOperationException(
                        $"Failed to connect to Redis within {_options.StartupConnectionTimeoutSeconds}s after {attempt} attempt(s). Worker cannot start.");
                }
            }
        }
    }
}
