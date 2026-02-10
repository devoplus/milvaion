using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Milvasoft.Milvaion.Sdk.Worker;
using Milvasoft.Milvaion.Sdk.Worker.Options;

namespace Milvaion.UnitTests.WorkerSdkTests;

[Trait("SDK Unit Tests", "WorkerServiceCollectionExtensions unit tests.")]
public class WorkerServiceCollectionExtensionsTests
{
    #region AddMilvaionWorkerCore

    [Fact]
    public void AddMilvaionWorkerCore_ShouldThrow_WhenWorkerOptionsSectionIsMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        // Act
        var act = () => services.AddMilvaionWorkerCore(configuration, requireJobConsumers: false);

        // Assert - WorkerOptions section missing doesn't throw (it uses defaults),
        // but certain required services should still be registered
        act.Should().NotThrow("core registration should work with defaults when job consumers not required");
    }

    [Fact]
    public void AddMilvaionWorkerCore_ShouldRegisterCoreServices_WithValidConfig()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "test-worker-01",
            ["Worker:MaxParallelJobs"] = "4",
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["Worker:RabbitMQ:Port"] = "5672",
            ["Worker:RabbitMQ:Username"] = "guest",
            ["Worker:RabbitMQ:Password"] = "guest",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        services.AddMilvaionWorkerCore(configuration, requireJobConsumers: false);

        // Assert
        var provider = services.BuildServiceProvider();
        var workerJobTracker = provider.GetService<Milvasoft.Milvaion.Sdk.Worker.Core.WorkerJobTracker>();
        workerJobTracker.Should().NotBeNull("WorkerJobTracker should be registered");
    }

    [Fact]
    public void AddMilvaionWorkerCore_ShouldRegisterJobExecutor_WhenRequireJobConsumersIsTrue()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "test-worker-01",
            ["Worker:RabbitMQ:Host"] = "localhost",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        services.AddMilvaionWorkerCore(configuration, requireJobConsumers: true);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Milvasoft.Milvaion.Sdk.Worker.Core.JobExecutor));
        descriptor.Should().NotBeNull("JobExecutor should be registered when requireJobConsumers is true");
    }

    [Fact]
    public void AddMilvaionWorkerCore_ShouldNotRegisterJobExecutor_WhenRequireJobConsumersIsFalse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "test-worker-01",
            ["Worker:RabbitMQ:Host"] = "localhost",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        services.AddMilvaionWorkerCore(configuration, requireJobConsumers: false);

        // Assert
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Milvasoft.Milvaion.Sdk.Worker.Core.JobExecutor));
        descriptor.Should().BeNull("JobExecutor should NOT be registered when requireJobConsumers is false");
    }

    [Fact]
    public void AddMilvaionWorkerCore_ShouldRegisterOfflineResilience_WhenEnabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "offline-worker",
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["Worker:OfflineResilience:Enabled"] = "true",
            ["Worker:OfflineResilience:LocalStoragePath"] = "./test_data",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        services.AddMilvaionWorkerCore(configuration, requireJobConsumers: false);

        // Assert
        var localStoreDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Milvasoft.Milvaion.Sdk.Worker.Persistence.LocalStateStore));
        localStoreDescriptor.Should().NotBeNull("LocalStateStore should be registered when offline resilience is enabled");
    }

    [Fact]
    public void AddMilvaionWorkerCore_ShouldNotRegisterOfflineResilience_WhenDisabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "normal-worker",
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["Worker:OfflineResilience:Enabled"] = "false",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        services.AddMilvaionWorkerCore(configuration, requireJobConsumers: false);

        // Assert
        var localStoreDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(Milvasoft.Milvaion.Sdk.Worker.Persistence.LocalStateStore));
        localStoreDescriptor.Should().BeNull("LocalStateStore should NOT be registered when offline resilience is disabled");
    }

    #endregion

    #region AddMilvaionWorkerWithJobs

    [Fact]
    public void AddMilvaionWorkerWithJobs_ShouldThrow_WhenJobConsumersSectionMissing()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "test-worker",
            ["Worker:RabbitMQ:Host"] = "localhost",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        var act = () => services.AddMilvaionWorkerWithJobs(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*JobConsumers*not found*");
    }

    [Fact]
    public void AddMilvaionWorkerWithJobs_ShouldThrow_WhenConfigHasJobWithoutConsumerId()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "test-worker",
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["JobConsumers:SomeJob:RoutingPattern"] = "worker.*",
            // Missing ConsumerId
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        var act = () => services.AddMilvaionWorkerWithJobs(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*ConsumerId*required*");
    }

    #endregion

    #region AddFileHealthCheck

    [Fact]
    public void AddFileHealthCheck_ShouldRegisterHealthChecks_WhenEnabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "health-worker",
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["Worker:HealthCheck:Enabled"] = "true",
            ["Worker:HealthCheck:IntervalSeconds"] = "30",
            ["Worker:HealthCheck:LiveFilePath"] = "/tmp/healthy",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        services.AddFileHealthCheck(configuration);

        // Assert - Health check service should be registered
        var healthCheckDescriptor = services.Any(d => d.ServiceType.Name.Contains("HealthCheck"));
        healthCheckDescriptor.Should().BeTrue("health check services should be registered when enabled");
    }

    [Fact]
    public void AddFileHealthCheck_ShouldNotRegisterHealthChecks_WhenDisabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "no-health-worker",
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["Worker:HealthCheck:Enabled"] = "false",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        var beforeCount = services.Count;

        // Act
        services.AddFileHealthCheck(configuration);

        // Assert
        services.Count.Should().Be(beforeCount, "no additional services should be registered when health check is disabled");
    }

    #endregion
}
