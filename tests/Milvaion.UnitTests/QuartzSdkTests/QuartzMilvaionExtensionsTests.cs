using FluentAssertions;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Extensions;
using Quartz;

namespace Milvaion.UnitTests.QuartzSdkTests;

[Trait("Quartz SDK Unit Tests", "QuartzMilvaionExtensions unit tests.")]
public class QuartzMilvaionExtensionsTests
{
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
}
