using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Milvaion.Infrastructure.LazyImpl;

namespace Milvaion.UnitTests.InfrastructureTests;

/// <summary>
/// Unit tests for MilvaionLazy.
/// Tests lazy service resolution from DI container.
/// </summary>
public class MilvaionLazyTests
{
    [Fact]
    public void Value_ShouldResolveServiceFromProvider()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();

        var lazy = new MilvaionLazy<ITestService>(provider);

        // Act
        var result = lazy.Value;

        // Assert
        result.Should().NotBeNull();
        result.Should().BeOfType<TestService>();
    }

    [Fact]
    public void Value_ShouldReturnSameInstanceOnMultipleCalls()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();

        var lazy = new MilvaionLazy<ITestService>(provider);

        // Act
        var result1 = lazy.Value;
        var result2 = lazy.Value;

        // Assert
        result1.Should().BeSameAs(result2);
    }

    [Fact]
    public void IsValueCreated_BeforeAccess_ShouldBeFalse()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();

        var lazy = new MilvaionLazy<ITestService>(provider);

        // Assert
        lazy.IsValueCreated.Should().BeFalse();
    }

    [Fact]
    public void IsValueCreated_AfterAccess_ShouldBeTrue()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        var provider = services.BuildServiceProvider();

        var lazy = new MilvaionLazy<ITestService>(provider);

        // Act
        _ = lazy.Value;

        // Assert
        lazy.IsValueCreated.Should().BeTrue();
    }

    [Fact]
    public void Value_WhenServiceNotRegistered_ShouldThrow()
    {
        // Arrange
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();

        var lazy = new MilvaionLazy<ITestService>(provider);

        // Act
        var act = () => lazy.Value;

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    private interface ITestService { }
    private class TestService : ITestService { }
}
