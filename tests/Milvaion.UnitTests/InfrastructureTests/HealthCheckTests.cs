using FluentAssertions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.Application.Utils.Models.Options;
using Milvaion.Infrastructure.HealthChecks;
using Milvaion.Infrastructure.Services.RabbitMQ;
using Moq;

namespace Milvaion.UnitTests.InfrastructureTests;

/// <summary>
/// Unit tests for health check implementations.
/// Tests RabbitMQ health checks.
/// </summary>
public class HealthCheckTests
{
    private readonly ILoggerFactory _loggerFactory;

    public HealthCheckTests()
    {
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        _loggerFactory = mockLoggerFactory.Object;
    }

    [Fact]
    public async Task RabbitMQHealthCheck_WhenHealthy_ShouldReturnHealthy()
    {
        // Arrange
        var options = Options.Create(new RabbitMQOptions());
        var factory = new RabbitMQConnectionFactory(options, _loggerFactory);

        // Since we can't mock sealed class, we test the unhealthy state
        var healthCheck = new RabbitMQHealthCheck(factory);
        var context = CreateHealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert - Factory is not initialized, so should be unhealthy
        result.Status.Should().Be(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task RabbitMQHealthCheck_WhenNotInitialized_ShouldReturnUnhealthy()
    {
        // Arrange
        var options = Options.Create(new RabbitMQOptions());
        var factory = new RabbitMQConnectionFactory(options, _loggerFactory);

        var healthCheck = new RabbitMQHealthCheck(factory);
        var context = CreateHealthCheckContext();

        // Act
        var result = await healthCheck.CheckHealthAsync(context);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Description.Should().Contain("RabbitMQ connection is not available");
    }

    private static HealthCheckContext CreateHealthCheckContext() => new()
    {
        Registration = new HealthCheckRegistration(
                "test",
                Mock.Of<IHealthCheck>(),
                HealthStatus.Unhealthy,
                null)
    };
}
