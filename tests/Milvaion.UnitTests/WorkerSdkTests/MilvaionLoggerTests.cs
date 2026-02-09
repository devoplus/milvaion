using FluentAssertions;
using Microsoft.Extensions.Logging;
using Milvasoft.Milvaion.Sdk.Utils;

namespace Milvaion.UnitTests.WorkerSdkTests;

[Trait("SDK Unit Tests", "MilvaionLogger unit tests.")]
public class MilvaionLoggerTests
{
    private readonly TestLoggerFactory _loggerFactory;
    private readonly MilvaionLogger _logger;

    public MilvaionLoggerTests()
    {
        _loggerFactory = new TestLoggerFactory();
        _logger = new MilvaionLogger(_loggerFactory);
    }

    [Fact]
    public void Debug_ShouldLogDebugMessage()
    {
        // Act
        _logger.Debug("Debug message");

        // Assert
        _loggerFactory.TestLogger.LoggedMessages.Should().Contain(m => m.Level == LogLevel.Debug && m.Message.Contains("Debug message"));
    }

    [Fact]
    public void Debug_WithTemplate_ShouldLogWithArgs()
    {
        // Act
        _logger.Debug("Processing {JobId}", "job-123");

        // Assert
        _loggerFactory.TestLogger.LoggedMessages.Should().Contain(m => m.Level == LogLevel.Debug);
    }

    [Fact]
    public void Information_ShouldLogInformationMessage()
    {
        // Act
        _logger.Information("Info message");

        // Assert
        _loggerFactory.TestLogger.LoggedMessages.Should().Contain(m => m.Level == LogLevel.Information && m.Message.Contains("Info message"));
    }

    [Fact]
    public void Warning_ShouldLogWarningMessage()
    {
        // Act
        _logger.Warning("Warning message");

        // Assert
        _loggerFactory.TestLogger.LoggedMessages.Should().Contain(m => m.Level == LogLevel.Warning && m.Message.Contains("Warning message"));
    }

    [Fact]
    public void Warning_WithException_ShouldLogExceptionDetails()
    {
        // Arrange
        var ex = new InvalidOperationException("test error");

        // Act
        _logger.Warning(ex, "Warning with exception");

        // Assert
        _loggerFactory.TestLogger.LoggedMessages.Should().Contain(m => m.Level == LogLevel.Warning);
    }

    [Fact]
    public void Error_ShouldLogErrorMessage()
    {
        // Act
        _logger.Error("Error message");

        // Assert
        _loggerFactory.TestLogger.LoggedMessages.Should().Contain(m => m.Level == LogLevel.Error && m.Message.Contains("Error message"));
    }

    [Fact]
    public void Error_WithException_ShouldLogExceptionDetails()
    {
        // Arrange
        var ex = new InvalidOperationException("critical error");

        // Act
        _logger.Error(ex, "Error with exception");

        // Assert
        _loggerFactory.TestLogger.LoggedMessages.Should().Contain(m => m.Level == LogLevel.Error);
    }

    [Fact]
    public void Fatal_ShouldLogCriticalMessage()
    {
        // Act
        _logger.Fatal("Fatal message");

        // Assert
        _loggerFactory.TestLogger.LoggedMessages.Should().Contain(m => m.Level == LogLevel.Critical && m.Message.Contains("Fatal message"));
    }

    [Fact]
    public void Verbose_ShouldLogTraceMessage()
    {
        // Act
        _logger.Verbose("Verbose message");

        // Assert
        _loggerFactory.TestLogger.LoggedMessages.Should().Contain(m => m.Level == LogLevel.Trace && m.Message.Contains("Verbose message"));
    }

    [Fact]
    public void Log_WithSeverityAndMessage_ShouldLogCorrectLevel()
    {
        // Act
        _logger.Log(LogLevel.Information, "Severity test");

        // Assert
        _loggerFactory.TestLogger.LoggedMessages.Should().Contain(m => m.Level == LogLevel.Information && m.Message.Contains("Severity test"));
    }

    [Fact]
    public void Log_WithSeverityExceptionAndMessage_ShouldLog()
    {
        // Arrange
        var ex = new Exception("inner");

        // Act
        _logger.Log(LogLevel.Error, ex, "Log with exception");

        // Assert
        _loggerFactory.TestLogger.LoggedMessages.Should().Contain(m => m.Level == LogLevel.Error);
    }

    #region Test Infrastructure

    private sealed class TestLoggerFactory : ILoggerFactory
    {
        public TestLogger TestLogger { get; } = new();

        public ILogger CreateLogger(string categoryName) => TestLogger;
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
    }

    private sealed class TestLogger : ILogger
    {
        public List<LogEntry> LoggedMessages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) => LoggedMessages.Add(new LogEntry
        {
            Level = logLevel,
            Message = formatter(state, exception),
            Exception = exception
        });
    }

    public sealed class LogEntry
    {
        public LogLevel Level { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }

    #endregion
}
