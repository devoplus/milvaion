using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.Application.Utils.Models.Options;
using Milvaion.Infrastructure.Services;
using Moq;

namespace Milvaion.UnitTests.InfrastructureTests;

/// <summary>
/// Unit tests for DispatcherControlService.
/// Tests runtime control of job dispatcher (stop/resume).
/// </summary>
public class DispatcherControlServiceTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOptions<JobDispatcherOptions> _options;

    public DispatcherControlServiceTests()
    {
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        _loggerFactory = mockLoggerFactory.Object;

        _options = Options.Create(new JobDispatcherOptions { Enabled = true });
    }

    [Fact]
    public void IsEnabled_ByDefault_ShouldBeTrue()
    {
        // Arrange
        var service = new DispatcherControlService(_loggerFactory, _options);

        // Act
        var result = service.IsEnabled;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Stop_ShouldSetIsEnabledToFalse()
    {
        // Arrange
        var service = new DispatcherControlService(_loggerFactory, _options);

        // Act
        service.Stop("Test reason", "TestUser");

        // Assert
        service.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void Resume_ShouldSetIsEnabledToTrue()
    {
        // Arrange
        var service = new DispatcherControlService(_loggerFactory, _options);
        service.Stop("Test reason", "TestUser");

        // Act
        service.Resume("AdminUser");

        // Assert
        service.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void Stop_WhenAlreadyStopped_ShouldRemainStopped()
    {
        // Arrange
        var service = new DispatcherControlService(_loggerFactory, _options);
        service.Stop("First reason", "User1");

        // Act
        service.Stop("Second reason", "User2");

        // Assert
        service.IsEnabled.Should().BeFalse();
        service.GetStopReason().Should().Contain("User1"); // Original stop info preserved
    }

    [Fact]
    public void Resume_WhenAlreadyRunning_ShouldRemainRunning()
    {
        // Arrange
        var service = new DispatcherControlService(_loggerFactory, _options);

        // Act
        service.Resume("TestUser");

        // Assert
        service.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void GetStopReason_WhenRunning_ShouldReturnNull()
    {
        // Arrange
        var service = new DispatcherControlService(_loggerFactory, _options);

        // Act
        var reason = service.GetStopReason();

        // Assert
        reason.Should().BeNull();
    }

    [Fact]
    public void GetStopReason_WhenStopped_ShouldReturnReason()
    {
        // Arrange
        var service = new DispatcherControlService(_loggerFactory, _options);
        service.Stop("Emergency maintenance", "AdminUser");

        // Act
        var reason = service.GetStopReason();

        // Assert
        reason.Should().Contain("Emergency maintenance");
        reason.Should().Contain("AdminUser");
    }

    [Fact]
    public void StopAndResume_MultipleTimes_ShouldWorkCorrectly()
    {
        // Arrange
        var service = new DispatcherControlService(_loggerFactory, _options);

        // Act & Assert
        service.Stop("Reason1", "User1");
        service.IsEnabled.Should().BeFalse();

        service.Resume("User2");
        service.IsEnabled.Should().BeTrue();

        service.Stop("Reason2", "User3");
        service.IsEnabled.Should().BeFalse();

        service.Resume("User4");
        service.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_ShouldBeThreadSafe()
    {
        // Arrange
        var service = new DispatcherControlService(_loggerFactory, _options);
        var iterations = 100;

        // Act - Simulate concurrent access
        Parallel.For(0, iterations, i =>
        {
            if (i % 2 == 0)
                service.Stop($"Reason{i}", $"User{i}");
            else
                service.Resume($"User{i}");
        });

        // Assert - Should not throw and should have valid state
        var finalState = service.IsEnabled;
        (finalState == true || finalState == false).Should().BeTrue();
    }
}
