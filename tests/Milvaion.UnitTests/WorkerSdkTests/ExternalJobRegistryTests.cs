using FluentAssertions;
using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using Milvasoft.Milvaion.Sdk.Worker.Utils;

namespace Milvaion.UnitTests.WorkerSdkTests;

[Trait("SDK Unit Tests", "ExternalJobRegistry unit tests.")]
public class ExternalJobRegistryTests
{
    [Fact]
    public void RegisterJob_ShouldRegisterNewJob()
    {
        // Arrange
        var registry = new ExternalJobRegistry();

        // Act
        registry.RegisterJob("external.email.job", typeof(FakeExternalJob));

        // Assert
        registry.Count.Should().Be(1);
        var configs = registry.GetJobConfigs();
        configs.Should().ContainKey("external.email.job");
        configs["external.email.job"].ConsumerId.Should().Be("external.email.job");
        configs["external.email.job"].JobType.Should().Be<FakeExternalJob>();
    }

    [Fact]
    public void RegisterJob_ShouldNotDuplicate_WhenSameExternalJobIdRegisteredTwice()
    {
        // Arrange
        var registry = new ExternalJobRegistry();
        registry.RegisterJob("external.email.job", typeof(FakeExternalJob));

        // Act
        registry.RegisterJob("external.email.job", typeof(FakeExternalJob));

        // Assert
        registry.Count.Should().Be(1);
    }

    [Fact]
    public void RegisterJob_ShouldRegisterMultipleDistinctJobs()
    {
        // Arrange
        var registry = new ExternalJobRegistry();

        // Act
        registry.RegisterJob("external.email.job", typeof(FakeExternalJob));
        registry.RegisterJob("external.report.job", typeof(FakeExternalJob));
        registry.RegisterJob("external.cleanup.job", typeof(FakeExternalJob));

        // Assert
        registry.Count.Should().Be(3);
        var configs = registry.GetJobConfigs();
        configs.Should().HaveCount(3);
    }

    [Fact]
    public void GetJobConfigs_ShouldReturnDefensiveCopy()
    {
        // Arrange
        var registry = new ExternalJobRegistry();
        registry.RegisterJob("external.email.job", typeof(FakeExternalJob));

        // Act
        var configs1 = registry.GetJobConfigs();
        configs1.Clear();
        var configs2 = registry.GetJobConfigs();

        // Assert - clearing first copy should not affect registry
        configs2.Should().HaveCount(1);
    }

    [Fact]
    public void Count_ShouldReturnZero_WhenEmpty()
    {
        // Arrange
        var registry = new ExternalJobRegistry();

        // Act & Assert
        registry.Count.Should().Be(0);
    }

    [Fact]
    public void RegisterJob_ShouldSetDefaultValues()
    {
        // Arrange
        var registry = new ExternalJobRegistry();

        // Act
        registry.RegisterJob("ext.job.1", typeof(FakeExternalJob));

        // Assert
        var config = registry.GetJobConfigs()["ext.job.1"];
        config.RoutingPattern.Should().Be("external.job.*");
        config.MaxParallelJobs.Should().Be(1);
    }

    private sealed class FakeExternalJob : IAsyncJob
    {
        public Task ExecuteAsync(IJobContext context) => Task.CompletedTask;
    }
}
