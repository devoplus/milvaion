using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Milvasoft.Core.Abstractions;
using Milvasoft.Milvaion.Sdk.Utils;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;

namespace Milvaion.IntegrationTests.TestBase;

/// <summary>
/// Lightweight fixture for Worker SDK tests that only need RabbitMQ and Redis.
/// No PostgreSQL, no WAF, no migrations — starts in seconds, not minutes.
/// </summary>
public class WorkerSdkContainerFixture : IAsyncLifetime
{
    private readonly RedisContainer _redisContainer = new RedisBuilder("redis:7-alpine")
        .WithCleanUp(true)
        .Build();

    private readonly RabbitMqContainer _rabbitMqContainer = new RabbitMqBuilder("rabbitmq:3-management-alpine")
        .WithUsername("guest")
        .WithPassword("guest")
        .WithCleanUp(true)
        .Build();

    private ServiceProvider _serviceProvider;

    public async Task InitializeAsync()
    {
        var startTasks = new List<Task>();

        if (_redisContainer.State != TestcontainersStates.Running)
            startTasks.Add(_redisContainer.StartAsync());

        if (_rabbitMqContainer.State != TestcontainersStates.Running)
            startTasks.Add(_rabbitMqContainer.StartAsync());

        await Task.WhenAll(startTasks);

        // Build a minimal service provider with ILoggerFactory and IMilvaLogger
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug));
        services.AddSingleton<IMilvaLogger>(sp => sp.GetRequiredService<ILoggerFactory>().CreateMilvaLogger<WorkerSdkContainerFixture>());

        _serviceProvider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        _serviceProvider?.Dispose();

        try
        {
            var stopTasks = new List<Task>
            {
                _redisContainer.StopAsync(),
                _rabbitMqContainer.StopAsync()
            };

            await Task.WhenAny(Task.WhenAll(stopTasks), Task.Delay(5000));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WorkerSdkContainerFixture dispose error: {ex.Message}");
        }
    }

    public string GetRedisConnectionString() => _redisContainer.GetConnectionString();
    public string GetRabbitMqHost() => _rabbitMqContainer.Hostname;
    public int GetRabbitMqPort() => _rabbitMqContainer.GetMappedPublicPort(5672);
    public IServiceProvider ServiceProvider => _serviceProvider;
}
