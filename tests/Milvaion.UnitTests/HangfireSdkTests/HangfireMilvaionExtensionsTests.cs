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

    private sealed class SampleHangfireJob
    {
        public void Execute() { }
        public void ProcessBatch() { }
    }
}
