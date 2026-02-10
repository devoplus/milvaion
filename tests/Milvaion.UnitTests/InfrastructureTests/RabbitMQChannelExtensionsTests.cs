using FluentAssertions;
using Milvaion.Infrastructure.Extensions;
using Milvasoft.Core.Abstractions;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Milvaion.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "RabbitMQChannelExtensions unit tests.")]
public class RabbitMQChannelExtensionsTests
{
    private readonly Mock<IMilvaLogger> _loggerMock = new();

    #region SafeAckAsync

    [Fact]
    public async Task SafeAckAsync_ShouldCallBasicAckAsync_WhenChannelIsOpen()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(false);
        channelMock.Setup(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                   .Returns(ValueTask.CompletedTask);

        using var semaphore = new SemaphoreSlim(1, 1);

        // Act
        await channelMock.Object.SafeAckAsync(42, semaphore, _loggerMock.Object, CancellationToken.None);

        // Assert
        channelMock.Verify(c => c.BasicAckAsync(42, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SafeAckAsync_ShouldSkip_WhenChannelIsClosed()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(true);

        using var semaphore = new SemaphoreSlim(1, 1);

        // Act
        await channelMock.Object.SafeAckAsync(42, semaphore, _loggerMock.Object, CancellationToken.None);

        // Assert
        channelMock.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SafeAckAsync_ShouldSkip_WhenCancellationRequested()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(false);

        using var semaphore = new SemaphoreSlim(1, 1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        await channelMock.Object.SafeAckAsync(42, semaphore, _loggerMock.Object, cts.Token);

        // Assert
        channelMock.Verify(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SafeAckAsync_ShouldHandleAlreadyClosedException()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(false);
        channelMock.Setup(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new RabbitMQ.Client.Exceptions.AlreadyClosedException(new ShutdownEventArgs(ShutdownInitiator.Application, 0, "test")));

        using var semaphore = new SemaphoreSlim(1, 1);

        // Act & Assert - Should not throw
        var act = () => channelMock.Object.SafeAckAsync(42, semaphore, _loggerMock.Object, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SafeAckAsync_ShouldReleaseSemaphore_EvenOnException()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(false);
        channelMock.Setup(c => c.BasicAckAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new Exception("Unexpected error"));

        using var semaphore = new SemaphoreSlim(1, 1);

        // Act
        await channelMock.Object.SafeAckAsync(42, semaphore, _loggerMock.Object, CancellationToken.None);

        // Assert - Semaphore should be available (released after exception)
        semaphore.CurrentCount.Should().Be(1);
    }

    #endregion

    #region SafeNackAsync

    [Fact]
    public async Task SafeNackAsync_ShouldCallBasicNackAsync_WhenChannelIsOpen()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(false);
        channelMock.Setup(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                   .Returns(ValueTask.CompletedTask);

        using var semaphore = new SemaphoreSlim(1, 1);

        // Act
        await channelMock.Object.SafeNackAsync(42, semaphore, _loggerMock.Object, CancellationToken.None);

        // Assert
        channelMock.Verify(c => c.BasicNackAsync(42, false, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SafeNackAsync_ShouldSkip_WhenChannelIsClosed()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(true);

        using var semaphore = new SemaphoreSlim(1, 1);

        // Act
        await channelMock.Object.SafeNackAsync(42, semaphore, _loggerMock.Object, CancellationToken.None);

        // Assert
        channelMock.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SafeNackAsync_ShouldPassRequeueParameter()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(false);
        channelMock.Setup(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                   .Returns(ValueTask.CompletedTask);

        using var semaphore = new SemaphoreSlim(1, 1);

        // Act
        await channelMock.Object.SafeNackAsync(42, semaphore, _loggerMock.Object, CancellationToken.None, requeue: true);

        // Assert
        channelMock.Verify(c => c.BasicNackAsync(42, false, true, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SafeNackAsync_ShouldHandleAlreadyClosedException()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(false);
        channelMock.Setup(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new RabbitMQ.Client.Exceptions.AlreadyClosedException(new ShutdownEventArgs(ShutdownInitiator.Application, 0, "test")));

        using var semaphore = new SemaphoreSlim(1, 1);

        // Act & Assert - Should not throw
        var act = () => channelMock.Object.SafeNackAsync(42, semaphore, _loggerMock.Object, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SafeNackAsync_ShouldSkip_WhenCancellationRequested()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(false);

        using var semaphore = new SemaphoreSlim(1, 1);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        await channelMock.Object.SafeNackAsync(42, semaphore, _loggerMock.Object, cts.Token);

        // Assert
        channelMock.Verify(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SafeNackAsync_ShouldReleaseSemaphore_EvenOnException()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(false);
        channelMock.Setup(c => c.BasicNackAsync(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                   .ThrowsAsync(new Exception("Unexpected error"));

        using var semaphore = new SemaphoreSlim(1, 1);

        // Act
        await channelMock.Object.SafeNackAsync(42, semaphore, _loggerMock.Object, CancellationToken.None);

        // Assert - Semaphore should be available (released after exception)
        semaphore.CurrentCount.Should().Be(1);
    }

    [Fact]
    public async Task SafeAckAsync_ShouldSkip_WhenChannelIsNull()
    {
        // Arrange
        IChannel channel = null;
        using var semaphore = new SemaphoreSlim(1, 1);

        // Act & Assert - Should not throw
        var act = () => channel.SafeAckAsync(42, semaphore, _loggerMock.Object, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SafeNackAsync_ShouldSkip_WhenChannelIsNull()
    {
        // Arrange
        IChannel channel = null;
        using var semaphore = new SemaphoreSlim(1, 1);

        // Act & Assert - Should not throw
        var act = () => channel.SafeNackAsync(42, semaphore, _loggerMock.Object, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    #endregion

    #region SafeCloseAsync (IChannel)

    [Fact]
    public async Task SafeCloseAsync_Channel_ShouldCloseAndDispose_WhenOpen()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(false);
        channelMock.Setup(c => c.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await channelMock.Object.SafeCloseAsync(_loggerMock.Object, CancellationToken.None);

        // Assert
        channelMock.Verify(c => c.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        channelMock.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task SafeCloseAsync_Channel_ShouldOnlyDispose_WhenAlreadyClosed()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(true);

        // Act
        await channelMock.Object.SafeCloseAsync(_loggerMock.Object, CancellationToken.None);

        // Assert
        channelMock.Verify(c => c.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        channelMock.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task SafeCloseAsync_Channel_ShouldNotThrow_WhenNull()
    {
        // Act & Assert
        IChannel channel = null;
        var act = () => channel.SafeCloseAsync(_loggerMock.Object, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SafeCloseAsync_Channel_ShouldStillDispose_WhenCloseThrows()
    {
        // Arrange
        var channelMock = new Mock<IChannel>();
        channelMock.Setup(c => c.IsClosed).Returns(false);
        channelMock.Setup(c => c.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Close failed"));

        // Act
        await channelMock.Object.SafeCloseAsync(_loggerMock.Object, CancellationToken.None);

        // Assert
        channelMock.Verify(c => c.Dispose(), Times.Once);
    }

    #endregion

    #region SafeCloseAsync (IConnection)

    [Fact]
    public async Task SafeCloseAsync_Connection_ShouldCloseAndDispose_WhenOpen()
    {
        // Arrange
        var connectionMock = new Mock<IConnection>();
        connectionMock.Setup(c => c.IsOpen).Returns(true);
        connectionMock.Setup(c => c.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await connectionMock.Object.SafeCloseAsync(_loggerMock.Object, CancellationToken.None);

        // Assert
        connectionMock.Verify(c => c.CloseAsync(It.IsAny<ushort>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Once);
        connectionMock.Verify(c => c.Dispose(), Times.Once);
    }

    [Fact]
    public async Task SafeCloseAsync_Connection_ShouldNotThrow_WhenNull()
    {
        IConnection connection = null;
        var act = () => connection.SafeCloseAsync(_loggerMock.Object, CancellationToken.None);
        await act.Should().NotThrowAsync();
    }

    #endregion
}
