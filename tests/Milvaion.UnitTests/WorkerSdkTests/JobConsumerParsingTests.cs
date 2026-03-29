using FluentAssertions;
using Milvasoft.Milvaion.Sdk.Worker.RabbitMQ;
using Moq;
using RabbitMQ.Client;
using System.Text;

namespace Milvaion.UnitTests.WorkerSdkTests;

[Trait("SDK Unit Tests", "JobConsumer ParseCorrelationId and ParseRetryCount unit tests.")]
public class JobConsumerParsingTests
{
    #region ParseCorrelationId

    [Fact]
    public void ParseCorrelationId_WithOccurrenceIdHeader_AsBytes_ShouldReturnGuid()
    {
        // Arrange
        var expectedId = Guid.CreateVersion7();
        var properties = CreateProperties(headers: new Dictionary<string, object>
        {
            ["OccurrenceId"] = Encoding.UTF8.GetBytes(expectedId.ToString())
        });

        // Act
        var result = JobConsumer.ParseCorrelationId(properties);

        // Assert
        result.Should().Be(expectedId);
    }

    [Fact]
    public void ParseCorrelationId_WithOccurrenceIdHeader_AsString_ShouldReturnGuid()
    {
        // Arrange
        var expectedId = Guid.CreateVersion7();
        var properties = CreateProperties(headers: new Dictionary<string, object>
        {
            ["OccurrenceId"] = expectedId.ToString()
        });

        // Act
        var result = JobConsumer.ParseCorrelationId(properties);

        // Assert
        result.Should().Be(expectedId);
    }

    [Fact]
    public void ParseCorrelationId_WithCorrelationIdHeader_AsBytes_ShouldReturnGuid()
    {
        // Arrange
        var expectedId = Guid.CreateVersion7();
        var properties = CreateProperties(headers: new Dictionary<string, object>
        {
            ["CorrelationId"] = Encoding.UTF8.GetBytes(expectedId.ToString())
        });

        // Act
        var result = JobConsumer.ParseCorrelationId(properties);

        // Assert
        result.Should().Be(expectedId);
    }

    [Fact]
    public void ParseCorrelationId_WithCorrelationIdHeader_AsString_ShouldReturnGuid()
    {
        // Arrange
        var expectedId = Guid.CreateVersion7();
        var properties = CreateProperties(headers: new Dictionary<string, object>
        {
            ["CorrelationId"] = expectedId.ToString()
        });

        // Act
        var result = JobConsumer.ParseCorrelationId(properties);

        // Assert
        result.Should().Be(expectedId);
    }

    [Fact]
    public void ParseCorrelationId_WithCorrelationIdProperty_ShouldReturnGuid()
    {
        // Arrange — no headers, but CorrelationId property set
        var expectedId = Guid.CreateVersion7();
        var properties = CreateProperties(correlationId: expectedId.ToString());

        // Act
        var result = JobConsumer.ParseCorrelationId(properties);

        // Assert
        result.Should().Be(expectedId);
    }

    [Fact]
    public void ParseCorrelationId_WithNoHeaders_AndNoCorrelationIdProperty_ShouldReturnEmpty()
    {
        // Arrange
        var properties = CreateProperties();

        // Act
        var result = JobConsumer.ParseCorrelationId(properties);

        // Assert
        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ParseCorrelationId_WithNullHeaders_ShouldFallbackToCorrelationIdProperty()
    {
        // Arrange
        var expectedId = Guid.CreateVersion7();
        var properties = CreateProperties(headers: null, correlationId: expectedId.ToString());

        // Act
        var result = JobConsumer.ParseCorrelationId(properties);

        // Assert
        result.Should().Be(expectedId);
    }

    [Fact]
    public void ParseCorrelationId_WithInvalidOccurrenceIdHeader_ShouldFallbackToCorrelationIdHeader()
    {
        // Arrange
        var expectedId = Guid.CreateVersion7();
        var properties = CreateProperties(headers: new Dictionary<string, object>
        {
            ["OccurrenceId"] = Encoding.UTF8.GetBytes("not-a-guid"),
            ["CorrelationId"] = Encoding.UTF8.GetBytes(expectedId.ToString())
        });

        // Act
        var result = JobConsumer.ParseCorrelationId(properties);

        // Assert
        result.Should().Be(expectedId);
    }

    [Fact]
    public void ParseCorrelationId_WithInvalidAllHeaders_ShouldReturnEmpty()
    {
        // Arrange
        var properties = CreateProperties(
            headers: new Dictionary<string, object>
            {
                ["OccurrenceId"] = Encoding.UTF8.GetBytes("bad"),
                ["CorrelationId"] = Encoding.UTF8.GetBytes("also-bad")
            },
            correlationId: "not-a-guid-either");

        // Act
        var result = JobConsumer.ParseCorrelationId(properties);

        // Assert
        result.Should().Be(Guid.Empty);
    }

    [Fact]
    public void ParseCorrelationId_OccurrenceIdHeader_HasPriority_OverCorrelationIdHeader()
    {
        // Arrange
        var occurrenceId = Guid.CreateVersion7();
        var correlationId = Guid.CreateVersion7();
        var properties = CreateProperties(headers: new Dictionary<string, object>
        {
            ["OccurrenceId"] = Encoding.UTF8.GetBytes(occurrenceId.ToString()),
            ["CorrelationId"] = Encoding.UTF8.GetBytes(correlationId.ToString())
        });

        // Act
        var result = JobConsumer.ParseCorrelationId(properties);

        // Assert
        result.Should().Be(occurrenceId, "OccurrenceId header should take priority over CorrelationId header");
    }

    [Fact]
    public void ParseCorrelationId_WithOccurrenceIdHeader_AsUnknownType_ShouldFallback()
    {
        // Arrange — value is an int (not byte[] or string)
        var expectedId = Guid.CreateVersion7();
        var properties = CreateProperties(
            headers: new Dictionary<string, object>
            {
                ["OccurrenceId"] = 12345
            },
            correlationId: expectedId.ToString());

        // Act
        var result = JobConsumer.ParseCorrelationId(properties);

        // Assert
        result.Should().Be(expectedId, "should fallback to CorrelationId property when OccurrenceId is unknown type");
    }

    #endregion

    #region ParseRetryCount

    [Fact]
    public void ParseRetryCount_WithNoHeaders_ShouldReturnZero()
    {
        // Arrange
        var properties = CreateProperties();

        // Act
        var result = JobConsumer.ParseRetryCount(properties);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ParseRetryCount_WithNullHeaders_ShouldReturnZero()
    {
        // Arrange
        var properties = CreateProperties(headers: null);

        // Act
        var result = JobConsumer.ParseRetryCount(properties);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ParseRetryCount_WithNoRetryCountHeader_ShouldReturnZero()
    {
        // Arrange
        var properties = CreateProperties(headers: new Dictionary<string, object>
        {
            ["SomeOtherHeader"] = "value"
        });

        // Act
        var result = JobConsumer.ParseRetryCount(properties);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ParseRetryCount_WithIntValue_ShouldReturnInt()
    {
        // Arrange
        var properties = CreateProperties(headers: new Dictionary<string, object>
        {
            ["x-retry-count"] = 3
        });

        // Act
        var result = JobConsumer.ParseRetryCount(properties);

        // Assert
        result.Should().Be(3);
    }

    [Fact]
    public void ParseRetryCount_WithLongValue_ShouldCastToInt()
    {
        // Arrange
        var properties = CreateProperties(headers: new Dictionary<string, object>
        {
            ["x-retry-count"] = 5L
        });

        // Act
        var result = JobConsumer.ParseRetryCount(properties);

        // Assert
        result.Should().Be(5);
    }

    [Fact]
    public void ParseRetryCount_WithByteArrayValue_ShouldConvertToInt()
    {
        // Arrange
        var bytes = BitConverter.GetBytes(7);
        var properties = CreateProperties(headers: new Dictionary<string, object>
        {
            ["x-retry-count"] = bytes
        });

        // Act
        var result = JobConsumer.ParseRetryCount(properties);

        // Assert
        result.Should().Be(7);
    }

    [Fact]
    public void ParseRetryCount_WithUnknownType_ShouldReturnZero()
    {
        // Arrange — value is a string (not byte[], int, or long)
        var properties = CreateProperties(headers: new Dictionary<string, object>
        {
            ["x-retry-count"] = "not-a-number"
        });

        // Act
        var result = JobConsumer.ParseRetryCount(properties);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ParseRetryCount_WithShortByteArray_ShouldReturnZero()
    {
        // Arrange — byte[] with less than 4 bytes
        var properties = CreateProperties(headers: new Dictionary<string, object>
        {
            ["x-retry-count"] = new byte[] { 1, 2 }
        });

        // Act
        var result = JobConsumer.ParseRetryCount(properties);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void ParseRetryCount_WithZeroIntValue_ShouldReturnZero()
    {
        // Arrange
        var properties = CreateProperties(headers: new Dictionary<string, object>
        {
            ["x-retry-count"] = 0
        });

        // Act
        var result = JobConsumer.ParseRetryCount(properties);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region Helpers

    private static IReadOnlyBasicProperties CreateProperties(
        IDictionary<string, object> headers = null,
        string correlationId = null)
    {
        var mock = new Mock<IReadOnlyBasicProperties>();

        mock.Setup(p => p.Headers).Returns(headers);
        mock.Setup(p => p.CorrelationId).Returns(correlationId);

        return mock.Object;
    }

    #endregion
}
