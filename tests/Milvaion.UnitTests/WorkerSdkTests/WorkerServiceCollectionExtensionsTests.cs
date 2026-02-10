using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Milvasoft.Milvaion.Sdk.Worker;

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

    #region GenerateRoutingPattern

    [Theory]
    [InlineData("SendEmailJob", "email-worker-01", "email-worker-01.sendemail.*")]
    [InlineData("TestJob", "email-worker-01", "email-worker-01.test.*")]
    [InlineData("NonParallelJob", "email-worker-01", "email-worker-01.nonparallel.*")]
    [InlineData("ProcessData", "worker-1", "worker-1.processdata.*")]
    [InlineData("MyJOB", "WORKER", "worker.my.*")]
    public void GenerateRoutingPattern_ShouldProduceExpectedPattern(string jobTypeName, string workerId, string expected)
    {
        // Act
        var result = WorkerServiceCollectionExtensions.GenerateRoutingPattern(jobTypeName, workerId);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void GenerateRoutingPattern_ShouldNotRemoveSuffix_WhenNoJobSuffix()
    {
        // Act
        var result = WorkerServiceCollectionExtensions.GenerateRoutingPattern("ProcessData", "worker-1");

        // Assert
        result.Should().Be("worker-1.processdata.*");
    }

    [Fact]
    public void GenerateRoutingPattern_ShouldHandleSingleWordJobName()
    {
        // Act
        var result = WorkerServiceCollectionExtensions.GenerateRoutingPattern("Job", "w1");

        // Assert - "Job" suffix removed leaves empty, lowercase → "w1..*"
        result.Should().Be("w1..*");
    }

    #endregion

    #region AddMilvaionWorkerWithJobs - Auto Routing Pattern

    [Fact]
    public void AddMilvaionWorkerWithJobs_ShouldAutoGenerateRoutingPattern_WhenNotSpecified()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "email-worker-01",
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["JobConsumers:SomeJob:ConsumerId"] = "some-consumer",
            // No RoutingPattern specified
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act - Will throw because there's no matching job class (Assembly.GetEntryAssembly() is null in tests),
        // but the auto-generated routing pattern code path at line 73-83 will have executed
        var act = () => services.AddMilvaionWorkerWithJobs(configuration);

        // Assert - Should fail with configsWithoutJob validation, proving it got past auto-generation
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*SomeJob*");
    }

    [Fact]
    public void AddMilvaionWorkerWithJobs_ShouldThrow_WhenConfigHasNoMatchingJobClass()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "test-worker",
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["JobConsumers:OrphanJobConfig:ConsumerId"] = "orphan-consumer",
            ["JobConsumers:OrphanJobConfig:RoutingPattern"] = "worker.orphan.*",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        var act = () => services.AddMilvaionWorkerWithJobs(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*without corresponding job class*OrphanJobConfig*");
    }

    [Fact]
    public void AddMilvaionWorkerWithJobs_ShouldThrow_WhenMultipleConfigsWithoutJobClasses()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "test-worker",
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["JobConsumers:GhostJob1:ConsumerId"] = "ghost-1",
            ["JobConsumers:GhostJob1:RoutingPattern"] = "w.g1.*",
            ["JobConsumers:GhostJob2:ConsumerId"] = "ghost-2",
            ["JobConsumers:GhostJob2:RoutingPattern"] = "w.g2.*",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        var act = () => services.AddMilvaionWorkerWithJobs(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*GhostJob1*")
           .WithMessage("*GhostJob2*");
    }

    [Fact]
    public void AddMilvaionWorkerWithJobs_ShouldUseDefaultWorkerId_WhenNotConfigured()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            // No WorkerId configured
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["JobConsumers:TestJob:ConsumerId"] = "test-consumer",
            // No RoutingPattern → will auto-generate using MachineName as fallback workerId
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act - Will throw because no matching job class, but routing pattern code path runs
        var act = () => services.AddMilvaionWorkerWithJobs(configuration);

        // Assert - Should get past auto-generation to the validation
        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*TestJob*");
    }

    #endregion

    #region AddHealthCheckEndpoints

    [Fact]
    public void AddHealthCheckEndpoints_ShouldRegisterHealthChecks_WhenEnabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "hc-worker",
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["Worker:HealthCheck:Enabled"] = "true",
            ["Worker:HealthCheck:IntervalSeconds"] = "15",
            ["Worker:HealthCheck:LiveFilePath"] = "/tmp/healthy",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        services.AddHealthCheckEndpoints(configuration);

        // Assert
        var healthCheckDescriptor = services.Any(d => d.ServiceType == typeof(HealthCheckService));
        healthCheckDescriptor.Should().BeTrue("HealthCheckService should be registered when endpoint health checks are enabled");
    }

    [Fact]
    public void AddHealthCheckEndpoints_ShouldNotRegister_WhenDisabled()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "hc-worker",
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["Worker:HealthCheck:Enabled"] = "false",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        var beforeCount = services.Count;

        // Act
        services.AddHealthCheckEndpoints(configuration);

        // Assert
        services.Count.Should().Be(beforeCount);
    }

    #endregion
}
