using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.Application.Utils.Models.Options;
using Milvaion.Infrastructure.Services.RabbitMQ;
using Moq;

namespace Milvaion.UnitTests.InfrastructureTests;

/// <summary>
/// Unit tests for RabbitMQConnectionFactory.
/// Tests initialization, channel creation, and health checks.
/// </summary>
public class RabbitMQConnectionFactoryTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IOptions<RabbitMQOptions> _options;

    public RabbitMQConnectionFactoryTests()
    {
        var mockLoggerFactory = new Mock<ILoggerFactory>();
        mockLoggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(Mock.Of<ILogger>());
        _loggerFactory = mockLoggerFactory.Object;

        _options = Options.Create(new RabbitMQOptions
        {
            Host = "localhost",
            Port = 5672,
            Username = "guest",
            Password = "guest",
            VirtualHost = "/",
            Heartbeat = 60,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = 10,
            ConnectionTimeout = 30,
            Durable = true,
            AutoDelete = false
        });
    }

    [Fact]
    public void Constructor_ShouldInitializeWithoutConnecting()
    {
        // Act
        var factory = new RabbitMQConnectionFactory(_options, _loggerFactory);

        // Assert
        factory.Should().NotBeNull();
        factory.IsHealthy().Should().BeFalse(); // Not initialized yet
    }

    [Fact]
    public void IsHealthy_WhenNotInitialized_ShouldReturnFalse()
    {
        // Arrange
        var factory = new RabbitMQConnectionFactory(_options, _loggerFactory);

        // Act
        var result = factory.IsHealthy();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public Task CreateChannelAsync_WhenNotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var factory = new RabbitMQConnectionFactory(_options, _loggerFactory);

        // Act
        var act = () => factory.CreateChannelAsync();

        // Assert
        return act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not initialized*");
    }

    [Fact]
    public void Connection_WhenNotInitialized_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var factory = new RabbitMQConnectionFactory(_options, _loggerFactory);

        // Act
        var act = () => factory.Connection;

        // Assert
        act.Should().Throw<InvalidOperationException>().WithMessage("*not initialized*");
    }

    [Fact]
    public Task DisposeAsync_WhenNotInitialized_ShouldNotThrow()
    {
        // Arrange
        var factory = new RabbitMQConnectionFactory(_options, _loggerFactory);

        // Act
        var act = () => factory.DisposeAsync().AsTask();

        // Assert
        return act.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_WhenNotInitialized_ShouldNotThrow()
    {
        // Arrange
        var factory = new RabbitMQConnectionFactory(_options, _loggerFactory);

        // Act
        var act = () => factory.Dispose();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task DisposeAsync_WhenCalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var factory = new RabbitMQConnectionFactory(_options, _loggerFactory);

        // Act
        await factory.DisposeAsync();
        var act = () => factory.DisposeAsync().AsTask();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void Dispose_WhenCalledMultipleTimes_ShouldBeIdempotent()
    {
        // Arrange
        var factory = new RabbitMQConnectionFactory(_options, _loggerFactory);

        // Act
        factory.Dispose();
        var act = () => factory.Dispose();

        // Assert
        act.Should().NotThrow();
    }
}
