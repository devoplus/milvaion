using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.TestBase;

/// <summary>
/// Base class for Worker SDK integration tests that only need RabbitMQ and/or Redis.
/// No database, no WAF, no migration overhead.
/// </summary>
public abstract class WorkerSdkTestBase(WorkerSdkContainerFixture fixture, ITestOutputHelper output)
{
    protected readonly WorkerSdkContainerFixture _fixture = fixture;
    protected readonly ITestOutputHelper _output = output;
    protected readonly IServiceProvider _serviceProvider = fixture.ServiceProvider;

    protected string GetRabbitMqHost() => _fixture.GetRabbitMqHost();
    protected int GetRabbitMqPort() => _fixture.GetRabbitMqPort();
    protected string GetRedisConnectionString() => _fixture.GetRedisConnectionString();

    protected ILoggerFactory GetLoggerFactory() => _serviceProvider.GetRequiredService<ILoggerFactory>();
}
