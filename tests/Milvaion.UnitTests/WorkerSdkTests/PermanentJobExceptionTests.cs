using FluentAssertions;
using Milvasoft.Milvaion.Sdk.Worker.Exceptions;

namespace Milvaion.UnitTests.WorkerSdkTests;

[Trait("SDK Unit Tests", "PermanentJobException unit tests.")]
public class PermanentJobExceptionTests
{
    [Fact]
    public void DefaultConstructor_ShouldCreateException()
    {
        // Act
        var ex = new PermanentJobException();

        // Assert
        ex.Should().NotBeNull();
        ex.Message.Should().NotBeNullOrEmpty();
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void MessageConstructor_ShouldSetMessage()
    {
        // Arrange
        var message = "Invalid job data format";

        // Act
        var ex = new PermanentJobException(message);

        // Assert
        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeNull();
    }

    [Fact]
    public void MessageAndInnerExceptionConstructor_ShouldSetBoth()
    {
        // Arrange
        var message = "Failed to parse job data";
        var inner = new FormatException("Bad format");

        // Act
        var ex = new PermanentJobException(message, inner);

        // Assert
        ex.Message.Should().Be(message);
        ex.InnerException.Should().BeSameAs(inner);
        ex.InnerException.Should().BeOfType<FormatException>();
    }

    [Fact]
    public void ShouldBeAssignableToException()
    {
        // Act
        var ex = new PermanentJobException("test");

        // Assert
        ex.Should().BeAssignableTo<Exception>();
    }
}
