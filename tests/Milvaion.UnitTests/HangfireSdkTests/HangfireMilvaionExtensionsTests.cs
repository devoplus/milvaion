using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Milvasoft.Milvaion.Sdk.Worker.Hangfire.Extensions;
using Milvasoft.Milvaion.Sdk.Worker.Hangfire.Filters;
using Milvasoft.Milvaion.Sdk.Worker.Hangfire.Services;
using Milvasoft.Milvaion.Sdk.Worker.Utils;

namespace Milvaion.UnitTests.HangfireSdkTests;

[Trait("Hangfire SDK Unit Tests", "HangfireMilvaionExtensions unit tests.")]
public class HangfireMilvaionExtensionsTests
{
    #region GetExternalJobId

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

    #endregion

    #region AddMilvaionHangfireIntegration

    [Fact]
    public void AddMilvaionHangfireIntegration_ShouldRegisterCoreServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        var config = new Dictionary<string, string>
        {
            ["Worker:WorkerId"] = "hangfire-worker",
            ["Worker:RabbitMQ:Host"] = "localhost",
            ["Worker:ExternalScheduler:Source"] = "Hangfire",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();

        // Act
        services.AddMilvaionHangfireIntegration(configuration);

        // Assert
        services.Any(d => d.ServiceType == typeof(IExternalJobPublisher)).Should().BeTrue();
        services.Any(d => d.ServiceType == typeof(ExternalJobRegistry)).Should().BeTrue();
        services.Any(d => d.ServiceType == typeof(MilvaionJobFilter)).Should().BeTrue();
    }

    #endregion

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "<Pending>")]
    private sealed class SampleHangfireJob
    {
        public void Execute() { }
        public void ProcessBatch() { }
    }
}
