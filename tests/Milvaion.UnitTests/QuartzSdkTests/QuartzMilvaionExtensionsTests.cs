using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Extensions;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Listeners;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Services;
using Milvasoft.Milvaion.Sdk.Worker.Utils;
using Quartz;

namespace Milvaion.UnitTests.QuartzSdkTests;

[Trait("Quartz SDK Unit Tests", "QuartzMilvaionExtensions unit tests.")]
public class QuartzMilvaionExtensionsTests
{
    #region GetExternalJobId

    [Fact]
    public void GetExternalJobId_ShouldReturnGroupDotName()
    {
        // Arrange
        var jobKey = new JobKey("SendEmailJob", "DEFAULT");

        // Act
        var result = jobKey.GetExternalJobId();

        // Assert
        result.Should().Be("DEFAULT.SendEmailJob");
    }

    [Fact]
    public void GetExternalJobId_WithCustomGroup_ShouldReturnGroupDotName()
    {
        // Arrange
        var jobKey = new JobKey("CleanupJob", "Maintenance");

        // Act
        var result = jobKey.GetExternalJobId();

        // Assert
        result.Should().Be("Maintenance.CleanupJob");
    }

    [Fact]
    public void GetExternalJobId_WithDefaultGroup_ShouldUseDefaultGroupName()
    {
        // Arrange
        var jobKey = new JobKey("MyJob");

        // Act
        var result = jobKey.GetExternalJobId();

        // Assert
        result.Should().Contain("MyJob");
    }

    #endregion

    #region AddMilvaionQuartzIntegration

    [Fact]
    public void AddMilvaionQuartzIntegration_ShouldRegisterCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "quartz-worker",
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["Worker:ExternalScheduler:Source"] = "Quartz",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        services.AddMilvaionQuartzIntegration(configuration);

        // Assert
        services.Any(d => d.ServiceType == typeof(IExternalJobPublisher)).Should().BeTrue();
        services.Any(d => d.ServiceType == typeof(ExternalJobRegistry)).Should().BeTrue();
        services.Any(d => d.ServiceType == typeof(MilvaionJobListener)).Should().BeTrue();
        services.Any(d => d.ServiceType == typeof(MilvaionSchedulerListener)).Should().BeTrue();
    }

    #endregion
}
