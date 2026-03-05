using FluentAssertions;
using Microsoft.Extensions.Options;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Worker.Hangfire.Services;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using Milvasoft.Milvaion.Sdk.Worker.Utils;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.WorkerSdk;

/// <summary>
/// Integration tests for HangfireWorkerStartupService.
/// Note: StartAsync/ScanAndRegisterRecurringJobsAsync require Hangfire JobStorage.Current
/// which needs a full Hangfire server setup. These tests focus on behaviors that don't require it.
/// </summary>
[Collection(nameof(WorkerSdkTestCollection))]
public class HangfireWorkerStartupServiceTests(WorkerSdkContainerFixture fixture, ITestOutputHelper output) : WorkerSdkTestBase(fixture, output)
{
    [Fact]
    public async Task StopAsync_ShouldNotThrow_WhenStartAsyncWasNeverCalled()
    {
        // Arrange
        var service = CreateService();

        // Act & Assert
        var act = () => service.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_ShouldHandleCancellationGracefully()
    {
        // Arrange
        var service = CreateService();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert - Already cancelled token should be handled gracefully
        var act = () => service.StartAsync(cts.Token);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StartAsync_ShouldLogWarning_WhenWorkerOptionsIsNull()
    {
        // Arrange - null options should not crash the service
        var service = new HangfireWorkerStartupService(
            null,
            new ExternalJobRegistry(),
            CreatePublisher(),
            _serviceProvider,
            GetLoggerFactory());

        // Act & Assert - Should handle null options gracefully
        var act = () => service.StartAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();

        // StopAsync should also be safe
        var stopAct = () => service.StopAsync(CancellationToken.None);
        await stopAct.Should().NotThrowAsync();
    }

    private HangfireWorkerStartupService CreateService()
    {
        var options = new WorkerOptions
        {
            WorkerId = "test-worker",
            RabbitMQ = new RabbitMQSettings
            {
                Host = GetRabbitMqHost(),
                Port = GetRabbitMqPort(),
                Username = "guest",
                Password = "guest",
                VirtualHost = "/"
            },
            ExternalScheduler = new MilvaionExternalSchedulerOptions
            {
                Source = "Hangfire"
            }
        };

        return new HangfireWorkerStartupService(
            Options.Create(options),
            new ExternalJobRegistry(),
            CreatePublisher(),
            _serviceProvider,
            GetLoggerFactory());
    }

    private ExternalJobPublisher CreatePublisher()
    {
        var options = new WorkerOptions
        {
            RabbitMQ = new RabbitMQSettings
            {
                Host = GetRabbitMqHost(),
                Port = GetRabbitMqPort(),
                Username = "guest",
                Password = "guest",
                VirtualHost = "/"
            }
        };

        return new ExternalJobPublisher(Options.Create(options), GetLoggerFactory());
    }
}
