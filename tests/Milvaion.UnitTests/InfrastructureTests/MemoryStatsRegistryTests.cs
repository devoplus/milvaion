using FluentAssertions;
using Milvaion.Application.Dtos.AdminDtos;
using Milvaion.Infrastructure.BackgroundServices.Base;

namespace Milvaion.UnitTests.InfrastructureTests;

/// <summary>
/// Unit tests for MemoryStatsRegistry.
/// Tests memory statistics tracking for background services.
/// </summary>
public class MemoryStatsRegistryTests
{
    [Fact]
    public void Register_ShouldAddStatsProvider()
    {
        // Arrange
        var registry = new MemoryStatsRegistry();
        var stats = new MemoryTrackStats { ServiceName = "TestService", IsRunning = true };

        // Act
        registry.Register("TestService", () => stats);

        // Assert
        var result = registry.GetStats("TestService");
        result.Should().NotBeNull();
        result.ServiceName.Should().Be("TestService");
    }

    [Fact]
    public void Unregister_ShouldRemoveStatsProvider()
    {
        // Arrange
        var registry = new MemoryStatsRegistry();
        var stats = new MemoryTrackStats { ServiceName = "TestService" };
        registry.Register("TestService", () => stats);

        // Act
        registry.Unregister("TestService");

        // Assert
        var result = registry.GetStats("TestService");
        result.Should().BeNull();
    }

    [Fact]
    public void GetStats_WhenNotRegistered_ShouldReturnNull()
    {
        // Arrange
        var registry = new MemoryStatsRegistry();

        // Act
        var result = registry.GetStats("NonExistentService");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetAggregatedStats_ShouldReturnAllRegisteredServices()
    {
        // Arrange
        var registry = new MemoryStatsRegistry();
        registry.Register("Service1", () => new MemoryTrackStats { ServiceName = "Service1", IsRunning = true });
        registry.Register("Service2", () => new MemoryTrackStats { ServiceName = "Service2", IsRunning = false });
        registry.Register("Service3", () => new MemoryTrackStats { ServiceName = "Service3", IsRunning = true, PotentialMemoryLeak = true });

        // Act
        var result = registry.GetAggregatedStats();

        // Assert
        result.Should().NotBeNull();
        result.ServiceStats.Should().HaveCount(3);
        result.RunningServicesCount.Should().Be(2);
        result.ServicesWithPotentialLeaks.Should().Be(1);
    }

    [Fact]
    public void GetAggregatedStats_ShouldIncludeProcessMemoryInfo()
    {
        // Arrange
        var registry = new MemoryStatsRegistry();

        // Act
        var result = registry.GetAggregatedStats();

        // Assert
        result.TotalManagedMemoryBytes.Should().BeGreaterThan(0);
        result.TotalProcessMemoryBytes.Should().BeGreaterThan(0);
        result.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetAggregatedStats_ShouldIncludeGCCollectionCounts()
    {
        // Arrange
        var registry = new MemoryStatsRegistry();

        // Act
        var result = registry.GetAggregatedStats();

        // Assert
        result.Gen0Collections.Should().BeGreaterOrEqualTo(0);
        result.Gen1Collections.Should().BeGreaterOrEqualTo(0);
        result.Gen2Collections.Should().BeGreaterOrEqualTo(0);
    }

    [Fact]
    public void Register_ShouldOverwriteExistingProvider()
    {
        // Arrange
        var registry = new MemoryStatsRegistry();
        registry.Register("TestService", () => new MemoryTrackStats { ServiceName = "OldValue" });

        // Act
        registry.Register("TestService", () => new MemoryTrackStats { ServiceName = "NewValue" });

        // Assert
        var result = registry.GetStats("TestService");
        result.ServiceName.Should().Be("NewValue");
    }

    [Fact]
    public void Registry_ShouldBeThreadSafe()
    {
        // Arrange
        var registry = new MemoryStatsRegistry();
        var iterations = 100;

        // Act - Simulate concurrent access
        Parallel.For(0, iterations, i =>
        {
            registry.Register($"Service{i}", () => new MemoryTrackStats { ServiceName = $"Service{i}" });
            _ = registry.GetStats($"Service{i}");
            _ = registry.GetAggregatedStats();
        });

        // Assert
        var result = registry.GetAggregatedStats();
        result.ServiceStats.Should().HaveCount(iterations);
    }
}
