using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Core.Abstractions;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using Milvasoft.Milvaion.Sdk.Utils;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using Milvasoft.Milvaion.Sdk.Worker.Persistence;
using Milvasoft.Milvaion.Sdk.Worker.RabbitMQ;
using Milvasoft.Milvaion.Sdk.Worker.Services;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.WorkerSdk;

/// <summary>
/// Integration tests for SyncOrchestratorService.
/// Tests periodic synchronization with real SQLite (LocalStateStore) and RabbitMQ (ConnectionMonitor).
/// </summary>
[Collection(nameof(WorkerSdkTestCollection))]
public class SyncOrchestratorServiceTests(WorkerSdkContainerFixture fixture, ITestOutputHelper output) : WorkerSdkTestBase(fixture, output)
{
    [Fact]
    public async Task ExecuteAsync_ShouldStopGracefully_WhenCancelled()
    {
        // Arrange
        await using var context = CreateServiceContext();
        using var cts = new CancellationTokenSource();

        // Act
        await context.Service.StartAsync(cts.Token);
        await Task.Delay(500); // Let it run one cycle

        cts.Cancel();

        // Assert - Should stop without throwing
        var act = () => context.Service.StopAsync(CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task StopAsync_ShouldAttemptFinalSync()
    {
        // Arrange
        await using var context = CreateServiceContext();
        using var cts = new CancellationTokenSource();

        await context.Service.StartAsync(cts.Token);
        await Task.Delay(300);

        // Store a pending status update before stopping
        await context.LocalStore.InitializeAsync();
        await context.LocalStore.StoreStatusUpdateAsync(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "test-worker", "test-instance",
            JobOccurrenceStatus.Completed);

        // Act - StopAsync should attempt final sync
        cts.Cancel();
        var act = () => context.Service.StopAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldSkipSync_WhenConnectionIsUnhealthy()
    {
        // Arrange - Use invalid RabbitMQ host so connection is always unhealthy
        var dbPath = Path.Combine(Path.GetTempPath(), $"milvaion_test_{Guid.CreateVersion7():N}");

        try
        {
            var loggerFactory = GetLoggerFactory();
            var logger = loggerFactory.CreateMilvaLogger<SyncOrchestratorService>();

            var localStore = new LocalStateStore(dbPath, loggerFactory);
            await localStore.InitializeAsync();

            var connectionMonitor = new ConnectionMonitor(new WorkerOptions
            {
                RabbitMQ = new RabbitMQSettings
                {
                    Host = "invalid-host",
                    Port = 5672,
                    Username = "guest",
                    Password = "guest",
                    VirtualHost = "/"
                }
            }, logger);

            var outboxService = new OutboxService(
                localStore,
                new StatusUpdatePublisher(CreateWorkerOptions("invalid-host", 5672), loggerFactory),
                new LogPublisher(CreateWorkerOptions("invalid-host", 5672), loggerFactory),
                connectionMonitor,
                logger);

            var workerOptions = new WorkerOptions
            {
                OfflineResilience = new OfflineResilienceSettings
                {
                    SyncIntervalSeconds = 1,
                    CleanupIntervalHours = 24,
                    RecordRetentionDays = 1
                }
            };

            var service = new SyncOrchestratorService(outboxService, localStore, connectionMonitor, logger, workerOptions);

            using var cts = new CancellationTokenSource();

            // Store pending data
            await localStore.StoreStatusUpdateAsync(
                Guid.CreateVersion7(), Guid.CreateVersion7(), "test-worker", "test-instance",
                JobOccurrenceStatus.Running);

            // Act
            await service.StartAsync(cts.Token);
            await Task.Delay(2000); // Let it run a couple of cycles

            cts.Cancel();
            await service.StopAsync(CancellationToken.None);

            // Assert - Data should still be pending since connection was unhealthy
            var stats = await localStore.GetStatsAsync();
            stats.PendingStatusUpdates.Should().BeGreaterThanOrEqualTo(1);

            connectionMonitor.Dispose();
            localStore.Dispose();
        }
        finally
        {
            CleanupTempDb(dbPath);
        }
    }

    private ServiceContext CreateServiceContext()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"milvaion_test_{Guid.CreateVersion7():N}");
        var loggerFactory = GetLoggerFactory();
        var logger = loggerFactory.CreateMilvaLogger<SyncOrchestratorService>();

        var workerOptions = CreateWorkerOptions(GetRabbitMqHost(), GetRabbitMqPort());

        var localStore = new LocalStateStore(dbPath, loggerFactory);

        var connectionMonitor = new ConnectionMonitor(new WorkerOptions
        {
            RabbitMQ = new RabbitMQSettings
            {
                Host = GetRabbitMqHost(),
                Port = GetRabbitMqPort(),
                Username = "guest",
                Password = "guest",
                VirtualHost = "/"
            }
        }, logger);

        var outboxService = new OutboxService(
            localStore,
            new StatusUpdatePublisher(workerOptions, loggerFactory),
            new LogPublisher(workerOptions, loggerFactory),
            connectionMonitor,
            logger);

        var options = new WorkerOptions
        {
            OfflineResilience = new OfflineResilienceSettings
            {
                SyncIntervalSeconds = 1,
                CleanupIntervalHours = 24,
                RecordRetentionDays = 1
            }
        };

        var service = new SyncOrchestratorService(outboxService, localStore, connectionMonitor, logger, options);

        return new ServiceContext(service, localStore, connectionMonitor, dbPath);
    }

    private static WorkerOptions CreateWorkerOptions(string host, int port) => new()
    {
        WorkerId = "test-worker",
        RabbitMQ = new RabbitMQSettings
        {
            Host = host,
            Port = port,
            Username = "guest",
            Password = "guest",
            VirtualHost = "/"
        }
    };

    private static void CleanupTempDb(string dbPath)
    {
        try
        {
            if (Directory.Exists(dbPath))
                Directory.Delete(dbPath, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private sealed class ServiceContext(
        SyncOrchestratorService service,
        LocalStateStore localStore,
        ConnectionMonitor connectionMonitor,
        string dbPath) : IAsyncDisposable
    {
        public SyncOrchestratorService Service { get; } = service;
        public LocalStateStore LocalStore { get; } = localStore;

        public async ValueTask DisposeAsync()
        {
            try
            {
                connectionMonitor.Dispose();
                localStore.Dispose();
            }
            catch
            {
                // Ignore
            }

            CleanupTempDb(dbPath);
        }
    }
}
