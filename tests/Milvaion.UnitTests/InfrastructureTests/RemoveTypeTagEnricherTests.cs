using FluentAssertions;
using Milvaion.Infrastructure.Logging;
using Serilog.Events;

namespace Milvaion.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "RemoveTypeTagEnricher unit tests.")]
public class RemoveTypeTagEnricherTests
{
    [Fact]
    public void RemoveTypeTags_ScalarValue_ShouldReturnSameValue()
    {
        // Arrange
        var scalarValue = new ScalarValue("test-value");

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(scalarValue);

        // Assert
        result.Should().Be(scalarValue);
    }

    [Fact]
    public void RemoveTypeTags_ScalarValueWithNumber_ShouldReturnSameValue()
    {
        // Arrange
        var scalarValue = new ScalarValue(42);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(scalarValue);

        // Assert
        result.Should().Be(scalarValue);
    }

    [Fact]
    public void RemoveTypeTags_EmptyStructureValue_ShouldReturnEmptyStructure()
    {
        // Arrange
        var structureValue = new StructureValue([]);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(structureValue);

        // Assert
        result.Should().BeOfType<StructureValue>();
        var resultStructure = (StructureValue)result;
        resultStructure.Properties.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTypeTags_StructureWithProperties_ShouldProcessAllProperties()
    {
        // Arrange
        var properties = new List<LogEventProperty>
        {
            new("Name", new ScalarValue("John")),
            new("Age", new ScalarValue(30))
        };
        var structureValue = new StructureValue(properties);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(structureValue);

        // Assert
        result.Should().BeOfType<StructureValue>();
        var resultStructure = (StructureValue)result;
        resultStructure.Properties.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveTypeTags_NestedStructure_ShouldProcessRecursively()
    {
        // Arrange
        var innerProperties = new List<LogEventProperty>
        {
            new("City", new ScalarValue("Istanbul")),
            new("Country", new ScalarValue("Turkey"))
        };
        var innerStructure = new StructureValue(innerProperties);

        var outerProperties = new List<LogEventProperty>
        {
            new("Name", new ScalarValue("John")),
            new("Address", innerStructure)
        };
        var outerStructure = new StructureValue(outerProperties);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(outerStructure);

        // Assert
        result.Should().BeOfType<StructureValue>();
        var resultStructure = (StructureValue)result;
        resultStructure.Properties.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveTypeTags_EmptySequence_ShouldReturnEmptySequence()
    {
        // Arrange
        var sequenceValue = new SequenceValue([]);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(sequenceValue);

        // Assert
        result.Should().BeOfType<SequenceValue>();
        var resultSequence = (SequenceValue)result;
        resultSequence.Elements.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTypeTags_SequenceWithScalarElements_ShouldProcessAllElements()
    {
        // Arrange
        var elements = new List<LogEventPropertyValue>
        {
            new ScalarValue("item1"),
            new ScalarValue("item2"),
            new ScalarValue("item3")
        };
        var sequenceValue = new SequenceValue(elements);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(sequenceValue);

        // Assert
        result.Should().BeOfType<SequenceValue>();
        var resultSequence = (SequenceValue)result;
        resultSequence.Elements.Should().HaveCount(3);
    }

    [Fact]
    public void RemoveTypeTags_SequenceWithStructureElements_ShouldProcessRecursively()
    {
        // Arrange
        var structure1 = new StructureValue([new LogEventProperty("Id", new ScalarValue(1))]);
        var structure2 = new StructureValue([new LogEventProperty("Id", new ScalarValue(2))]);
        var sequenceValue = new SequenceValue([structure1, structure2]);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(sequenceValue);

        // Assert
        result.Should().BeOfType<SequenceValue>();
        var resultSequence = (SequenceValue)result;
        resultSequence.Elements.Should().HaveCount(2);
        resultSequence.Elements.Should().AllBeOfType<StructureValue>();
    }

    [Fact]
    public void RemoveTypeTags_NullScalarValue_ShouldReturnSameValue()
    {
        // Arrange
        var scalarValue = new ScalarValue(null);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(scalarValue);

        // Assert
        result.Should().Be(scalarValue);
    }

    [Fact]
    public void RemoveTypeTags_BooleanScalarValue_ShouldReturnSameValue()
    {
        // Arrange
        var scalarValue = new ScalarValue(true);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(scalarValue);

        // Assert
        result.Should().Be(scalarValue);
    }

    [Fact]
    public void RemoveTypeTags_DateTimeScalarValue_ShouldReturnSameValue()
    {
        // Arrange
        var dateTime = DateTime.UtcNow;
        var scalarValue = new ScalarValue(dateTime);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(scalarValue);

        // Assert
        result.Should().Be(scalarValue);
    }

    [Fact]
    public void RemoveTypeTags_MixedSequence_ShouldProcessCorrectly()
    {
        // Arrange
        var elements = new List<LogEventPropertyValue>
        {
            new ScalarValue("string"),
            new ScalarValue(42),
            new StructureValue([new LogEventProperty("Key", new ScalarValue("Value"))])
        };
        var sequenceValue = new SequenceValue(elements);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(sequenceValue);

        // Assert
        result.Should().BeOfType<SequenceValue>();
        var resultSequence = (SequenceValue)result;
        resultSequence.Elements.Should().HaveCount(3);
    }

    [Fact]
    public void Enrich_LogEventWithScalarProperties_ShouldNotModify()
    {
        // Arrange
        var enricher = new RemoveTypeTagEnricher();
        var properties = new List<LogEventProperty>
        {
            new("Message", new ScalarValue("Test message")),
            new("Level", new ScalarValue("Information"))
        };
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplate([]),
            properties);

        // Act
        enricher.Enrich(logEvent, null);

        // Assert - Should complete without error
        logEvent.Properties.Should().HaveCount(2);
    }

    [Fact]
    public void Enrich_LogEventWithStructureProperty_ShouldProcess()
    {
        // Arrange
        var enricher = new RemoveTypeTagEnricher();
        var structureValue = new StructureValue([
            new LogEventProperty("Name", new ScalarValue("Test")),
            new LogEventProperty("Value", new ScalarValue(123))
        ]);
        var properties = new List<LogEventProperty>
        {
            new("Data", structureValue)
        };
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Information,
            null,
            new MessageTemplate([]),
            properties);

        // Act
        enricher.Enrich(logEvent, null);

        // Assert
        logEvent.Properties.Should().ContainKey("Data");
        logEvent.Properties["Data"].Should().BeOfType<StructureValue>();
    }

    [Fact]
    public void Enrich_EmptyLogEvent_ShouldNotThrow()
    {
        // Arrange
        var enricher = new RemoveTypeTagEnricher();
        var logEvent = new LogEvent(
            DateTimeOffset.UtcNow,
            LogEventLevel.Debug,
            null,
            new MessageTemplate([]),
            []);

        // Act
        var action = () => enricher.Enrich(logEvent, null);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void RemoveTypeTags_WithScalarValue_ShouldReturnSameValue()
    {
        // Arrange
        var scalarValue = new ScalarValue("test");

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(scalarValue);

        // Assert
        result.Should().Be(scalarValue);
    }

    [Fact]
    public void RemoveTypeTags_WithSequenceValue_ShouldProcessElements()
    {
        // Arrange
        var elements = new LogEventPropertyValue[]
        {
            new ScalarValue("item1"),
            new ScalarValue("item2")
        };
        var sequenceValue = new SequenceValue(elements);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(sequenceValue);

        // Assert
        result.Should().BeOfType<SequenceValue>();
        var seq = (SequenceValue)result;
        seq.Elements.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveTypeTags_WithStructureValue_ShouldProcessProperties()
    {
        // Arrange
        var properties = new[]
        {
            new LogEventProperty("Name", new ScalarValue("TestValue")),
            new LogEventProperty("Count", new ScalarValue(42))
        };
        var structureValue = new StructureValue(properties);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(structureValue);

        // Assert
        result.Should().BeOfType<StructureValue>();
        var structure = (StructureValue)result;
        structure.Properties.Should().HaveCount(2);
    }

    [Fact]
    public void RemoveTypeTags_WithNestedStructure_ShouldProcessRecursively()
    {
        // Arrange
        var innerProperties = new[]
        {
            new LogEventProperty("InnerProp", new ScalarValue("InnerValue"))
        };
        var innerStructure = new StructureValue(innerProperties);

        var outerProperties = new[]
        {
            new LogEventProperty("OuterProp", innerStructure)
        };
        var outerStructure = new StructureValue(outerProperties);

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(outerStructure);

        // Assert
        result.Should().BeOfType<StructureValue>();
        var structure = (StructureValue)result;
        structure.Properties.Should().HaveCount(1);
    }

    [Fact]
    public void RemoveTypeTags_WithNullScalar_ShouldNotThrow()
    {
        // Arrange
        var nullValue = new ScalarValue(null);

        // Act
        var act = () => RemoveTypeTagEnricher.RemoveTypeTags(nullValue);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void RemoveTypeTags_WithEmptySequence_ShouldReturnEmptySequence()
    {
        // Arrange
        var emptySequence = new SequenceValue(Array.Empty<LogEventPropertyValue>());

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(emptySequence);

        // Assert
        result.Should().BeOfType<SequenceValue>();
        var seq = (SequenceValue)result;
        seq.Elements.Should().BeEmpty();
    }

    [Fact]
    public void RemoveTypeTags_WithEmptyStructure_ShouldReturnEmptyStructure()
    {
        // Arrange
        var emptyStructure = new StructureValue(Array.Empty<LogEventProperty>());

        // Act
        var result = RemoveTypeTagEnricher.RemoveTypeTags(emptyStructure);

        // Assert
        result.Should().BeOfType<StructureValue>();
        var structure = (StructureValue)result;
        structure.Properties.Should().BeEmpty();
    }
}
