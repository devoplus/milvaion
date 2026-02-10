using FluentAssertions;
using Milvasoft.Milvaion.Sdk.Worker.Hangfire.Extensions;

namespace Milvaion.UnitTests.HangfireSdkTests;

[Trait("Hangfire SDK Unit Tests", "HangfireMilvaionExtensions unit tests.")]
public class HangfireMilvaionExtensionsTests
{
    [Fact]
    public void GetExternalJobId_ShouldReturnTypeNameDotMethodName()
    {
        // Act
        var result = typeof(SampleHangfireJob).GetExternalJobId("Execute");

        // Assert
        result.Should().Be("SampleHangfireJob.Execute");
    }

    [Fact]
    public void GetExternalJobId_WithCustomMethod_ShouldIncludeMethodName()
    {
        // Act
        var result = typeof(SampleHangfireJob).GetExternalJobId("ProcessBatch");

        // Assert
        result.Should().Be("SampleHangfireJob.ProcessBatch");
    }

    [Fact]
    public void GetExternalJobId_ShouldUseShortTypeName()
    {
        // Act
        var result = typeof(SampleHangfireJob).GetExternalJobId("Execute");

        // Assert - should not contain namespace
        result.Should().NotContain("Milvaion.UnitTests");
        result.Should().StartWith("SampleHangfireJob");
    }

    [Fact]
    public void GetExternalJobId_ShouldThrow_WhenTypeIsNull()
    {
        // Act & Assert
        Type nullType = null;
        var act = () => nullType.GetExternalJobId("Execute");
        act.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void GetExternalJobId_ShouldHandleNullMethodName()
    {
        // Act
        var result = typeof(SampleHangfireJob).GetExternalJobId(null);

        // Assert
        result.Should().Be("SampleHangfireJob.");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
    private sealed class SampleHangfireJob
    {
        public void Execute() { }
        public void ProcessBatch() { }
    }
}
