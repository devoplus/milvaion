using FluentAssertions;
using Milvasoft.Milvaion.Sdk.Worker.HealthChecks;

namespace Milvaion.UnitTests.WorkerSdkTests;

[Trait("SDK Unit Tests", "HealthCheckModels unit tests.")]
public class HealthCheckModelsTests
{
    [Fact]
    public void HealthCheckResponse_ShouldSetAllProperties()
    {
        // Arrange & Act
        var response = new HealthCheckResponse
        {
            Status = "Healthy",
            Duration = TimeSpan.FromMilliseconds(150),
            Timestamp = DateTime.UtcNow,
            Checks =
            [
                new HealthCheckEntry
                {
                    Name = "Redis",
                    Status = "Healthy",
                    Description = "Redis is connected",
                    Duration = TimeSpan.FromMilliseconds(5),
                    Tags = ["cache", "redis"],
                    Data = new Dictionary<string, string> { ["LatencyMs"] = "5" }
                }
            ]
        };

        // Assert
        response.Status.Should().Be("Healthy");
        response.Duration.TotalMilliseconds.Should().Be(150);
        response.Checks.Should().HaveCount(1);
        response.Checks[0].Name.Should().Be("Redis");
        response.Checks[0].Tags.Should().Contain("cache");
        response.Checks[0].Data.Should().ContainKey("LatencyMs");
    }

    [Fact]
    public void HealthCheckResponse_DefaultChecks_ShouldBeEmpty()
    {
        // Act
        var response = new HealthCheckResponse
        {
            Status = "Degraded"
        };

        // Assert
        response.Checks.Should().BeEmpty();
    }

    [Fact]
    public void HealthCheckEntry_DefaultCollections_ShouldBeEmpty()
    {
        // Act
        var entry = new HealthCheckEntry
        {
            Name = "Test",
            Status = "Healthy"
        };

        // Assert
        entry.Tags.Should().BeEmpty();
        entry.Data.Should().BeEmpty();
        entry.Description.Should().BeNull();
    }

    [Fact]
    public void LivenessResponse_ShouldSetAllProperties()
    {
        // Arrange & Act
        var response = new LivenessResponse
        {
            Status = "Alive",
            Timestamp = DateTime.UtcNow,
            Uptime = TimeSpan.FromHours(2)
        };

        // Assert
        response.Status.Should().Be("Alive");
        response.Uptime.TotalHours.Should().Be(2);
        response.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
