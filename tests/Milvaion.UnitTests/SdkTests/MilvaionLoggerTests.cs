using FluentAssertions;
using Microsoft.Extensions.Logging;
using Milvasoft.Milvaion.Sdk.Utils;
using Moq;

namespace Milvaion.UnitTests.SdkTests;

[Trait("SDK Unit Tests", "MilvaionLogger unit tests.")]
#pragma warning disable CA1873 // Avoid potentially expensive logging
public class MilvaionLoggerTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly MilvaionLogger _milvaionLogger;

    public MilvaionLoggerTests()
    {
        _loggerMock = new Mock<ILogger>();
        _loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

        var loggerFactoryMock = new Mock<ILoggerFactory>();
        loggerFactoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_loggerMock.Object);

        _milvaionLogger = new MilvaionLogger(loggerFactoryMock.Object);
    }

    #region Debug

    [Fact]
    public void Debug_WithMessage_ShouldCallLogDebug()
    {
        _milvaionLogger.Debug("test debug");
        _loggerMock.Verify(x => x.Log(LogLevel.Debug, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Debug_WithTemplate_ShouldCallLogDebug()
    {
        _milvaionLogger.Debug("test {Value}", "param1");
        _loggerMock.Verify(x => x.Log(LogLevel.Debug, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Debug_WithException_ShouldCallLogDebug()
    {
        var ex = new Exception("test");
        _milvaionLogger.Debug(ex, "failed");
        _loggerMock.Verify(x => x.Log(LogLevel.Debug, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), ex, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Debug_WithExceptionAndTemplate_ShouldCallLogDebug()
    {
        var ex = new Exception("test");
        _milvaionLogger.Debug(ex, "failed {Code}", 500);
        _loggerMock.Verify(x => x.Log(LogLevel.Debug, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), ex, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    #endregion

    #region Information

    [Fact]
    public void Information_WithMessage_ShouldCallLogInformation()
    {
        _milvaionLogger.Information("info msg");
        _loggerMock.Verify(x => x.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Information_WithTemplate_ShouldCallLogInformation()
    {
        _milvaionLogger.Information("info {Key}", "val");
        _loggerMock.Verify(x => x.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Information_WithException_ShouldCallLogInformation()
    {
        var ex = new Exception("test");
        _milvaionLogger.Information(ex, "failed");
        _loggerMock.Verify(x => x.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), ex, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Information_WithExceptionAndTemplate_ShouldCallLogInformation()
    {
        var ex = new Exception("test");
        _milvaionLogger.Information(ex, "failed {Code}", 400);
        _loggerMock.Verify(x => x.Log(LogLevel.Information, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), ex, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    #endregion

    #region Warning

    [Fact]
    public void Warning_WithMessage_ShouldCallLogWarning()
    {
        _milvaionLogger.Warning("warn msg");
        _loggerMock.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Warning_WithTemplate_ShouldCallLogWarning()
    {
        _milvaionLogger.Warning("warn {Key}", "val");
        _loggerMock.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Warning_WithException_ShouldCallLogWarning()
    {
        var ex = new Exception("test");
        _milvaionLogger.Warning(ex, "warn ex");
        _loggerMock.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), ex, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Warning_WithExceptionAndTemplate_ShouldCallLogWarning()
    {
        var ex = new Exception("test");
        _milvaionLogger.Warning(ex, "warn {Detail}", "info");
        _loggerMock.Verify(x => x.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), ex, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    #endregion

    #region Error

    [Fact]
    public void Error_WithMessage_ShouldCallLogError()
    {
        _milvaionLogger.Error("error msg");
        _loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Error_WithTemplate_ShouldCallLogError()
    {
        _milvaionLogger.Error("error {Key}", "val");
        _loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Error_WithException_ShouldCallLogError()
    {
        var ex = new Exception("fail");
        _milvaionLogger.Error(ex, "error ex");
        _loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), ex, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Error_WithExceptionAndTemplate_ShouldCallLogError()
    {
        var ex = new Exception("fail");
        _milvaionLogger.Error(ex, "error {Detail}", "info");
        _loggerMock.Verify(x => x.Log(LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), ex, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    #endregion

    #region Fatal

    [Fact]
    public void Fatal_WithMessage_ShouldCallLogCritical()
    {
        _milvaionLogger.Fatal("fatal msg");
        _loggerMock.Verify(x => x.Log(LogLevel.Critical, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Fatal_WithTemplate_ShouldCallLogCritical()
    {
        _milvaionLogger.Fatal("fatal {Key}", "val");
        _loggerMock.Verify(x => x.Log(LogLevel.Critical, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Fatal_WithException_ShouldCallLogCritical()
    {
        var ex = new Exception("fatal");
        _milvaionLogger.Fatal(ex, "fatal ex");
        _loggerMock.Verify(x => x.Log(LogLevel.Critical, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), ex, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Fatal_WithExceptionAndTemplate_ShouldCallLogCritical()
    {
        var ex = new Exception("fatal");
        _milvaionLogger.Fatal(ex, "fatal {Detail}", "info");
        _loggerMock.Verify(x => x.Log(LogLevel.Critical, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), ex, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    #endregion

    #region Verbose

    [Fact]
    public void Verbose_WithMessage_ShouldCallLogTrace()
    {
        _milvaionLogger.Verbose("trace msg");
        _loggerMock.Verify(x => x.Log(LogLevel.Trace, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Verbose_WithTemplate_ShouldCallLogTrace()
    {
        _milvaionLogger.Verbose("trace {Key}", "val");
        _loggerMock.Verify(x => x.Log(LogLevel.Trace, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), null, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Verbose_WithException_ShouldCallLogTrace()
    {
        var ex = new Exception("trace");
        _milvaionLogger.Verbose(ex, "trace ex");
        _loggerMock.Verify(x => x.Log(LogLevel.Trace, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), ex, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    [Fact]
    public void Verbose_WithExceptionAndTemplate_ShouldCallLogTrace()
    {
        var ex = new Exception("trace");
        _milvaionLogger.Verbose(ex, "trace {Detail}", "info");
        _loggerMock.Verify(x => x.Log(LogLevel.Trace, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), ex, It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Once);
    }

    #endregion

    #region Log / LogAsync

    [Fact]
    public void Log_WithJsonEntry_ShouldNotThrow()
    {
        var json = """{"TransactionId":"tx1","Namespace":"ns","ClassName":"cls","MethodName":"m","IsSuccess":true}""";
        var act = () => _milvaionLogger.Log(json);
        act.Should().NotThrow();
    }

    [Fact]
    public void LogAsync_WithJsonEntry_ShouldNotThrow()
    {
        var json = """{"TransactionId":"tx1","Namespace":"ns","ClassName":"cls","MethodName":"m","IsSuccess":true}""";
        var act = async () => await _milvaionLogger.LogAsync(json);
        act.Should().NotThrowAsync();
    }

    [Fact]
    public void Log_ShouldNotLog_WhenLogLevelDisabled()
    {
        // Arrange
        var loggerMock = new Mock<ILogger>();
        loggerMock.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(false);

        var factoryMock = new Mock<ILoggerFactory>();
        factoryMock.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(loggerMock.Object);

        var logger = new MilvaionLogger(factoryMock.Object);

        // Act
        logger.Debug("should not log");
        logger.Information("should not log");
        logger.Warning("should not log");
        logger.Error("should not log");
        logger.Fatal("should not log");
        logger.Verbose("should not log");

        // Assert - Log should never be called since IsEnabled returns false
        loggerMock.Verify(x => x.Log(It.IsAny<LogLevel>(), It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(), It.IsAny<Func<It.IsAnyType, Exception, string>>()), Times.Never);
    }

    #endregion
}
#pragma warning restore CA1873 // Avoid potentially expensive logging
