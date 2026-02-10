using FluentAssertions;
using Milvaion.Infrastructure.Services;
using System.Reflection;

namespace Milvaion.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "ExportService unit tests.")]
public class ExportServiceTests
{
    private static readonly MethodInfo _getNestedPropertyValueMethod = typeof(ExportService)
        .GetMethod("GetNestedPropertyValue", BindingFlags.NonPublic | BindingFlags.Static);

    private static readonly MethodInfo _findDateTimeColumnsMethod = typeof(ExportService)
        .GetMethod("FindDateTimeColumns", BindingFlags.NonPublic | BindingFlags.Static);

    #region GetNestedPropertyValue

    [Fact]
    public void GetNestedPropertyValue_ShouldReturnNull_WhenObjectIsNull()
    {
        var result = _getNestedPropertyValueMethod.Invoke(null, [null, "Name"]);
        result.Should().BeNull();
    }

    [Fact]
    public void GetNestedPropertyValue_ShouldReturnNull_WhenPropPathIsNull()
    {
        var obj = new { Name = "Test" };
        var result = _getNestedPropertyValueMethod.Invoke(null, [obj, null]);
        result.Should().BeNull();
    }

    [Fact]
    public void GetNestedPropertyValue_ShouldReturnNull_WhenPropPathIsEmpty()
    {
        var obj = new { Name = "Test" };
        var result = _getNestedPropertyValueMethod.Invoke(null, [obj, ""]);
        result.Should().BeNull();
    }

    [Fact]
    public void GetNestedPropertyValue_ShouldReturnSimpleProperty()
    {
        var obj = new TestItem { Name = "TestValue" };
        var result = _getNestedPropertyValueMethod.Invoke(null, [obj, "Name"]);
        result.Should().Be("TestValue");
    }

    [Fact]
    public void GetNestedPropertyValue_ShouldReturnNestedProperty()
    {
        var obj = new TestItem
        {
            Name = "Parent",
            Nested = new NestedItem { Value = "NestedValue" }
        };

        var result = _getNestedPropertyValueMethod.Invoke(null, [obj, "Nested.Value"]);
        result.Should().Be("NestedValue");
    }

    [Fact]
    public void GetNestedPropertyValue_ShouldReturnNull_WhenPropertyDoesNotExist()
    {
        var obj = new TestItem { Name = "Test" };
        var result = _getNestedPropertyValueMethod.Invoke(null, [obj, "NonExistent"]);
        result.Should().BeNull();
    }

    [Fact]
    public void GetNestedPropertyValue_ShouldReturnNull_WhenNestedObjectIsNull()
    {
        var obj = new TestItem { Name = "Test", Nested = null };
        var result = _getNestedPropertyValueMethod.Invoke(null, [obj, "Nested.Value"]);
        result.Should().BeNull();
    }

    #endregion

    #region FindDateTimeColumns

    [Fact]
    public void FindDateTimeColumns_ShouldReturnDateTimeColumnIndexes()
    {
        var properties = typeof(DateTimeTestItem).GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();

        var result = (IEnumerable<int>)_findDateTimeColumnsMethod.Invoke(null, [properties]);
        var indexes = result.ToList();

        // DateTimeTestItem has: Name(string), CreatedAt(DateTime), UpdatedAt(DateTime?), Offset(DateTimeOffset), NullableOffset(DateTimeOffset?)
        // Indexes: 0=Name, 1=CreatedAt, 2=UpdatedAt, 3=Offset, 4=NullableOffset
        indexes.Should().Contain(1); // CreatedAt (DateTime)
        indexes.Should().Contain(2); // UpdatedAt (DateTime?)
        indexes.Should().Contain(3); // Offset (DateTimeOffset)
        indexes.Should().Contain(4); // NullableOffset (DateTimeOffset?)
        indexes.Should().NotContain(0); // Name (string)
    }

    [Fact]
    public void FindDateTimeColumns_ShouldReturnEmpty_WhenNoDateTimeColumns()
    {
        var properties = typeof(NoDateItem).GetProperties(BindingFlags.Public | BindingFlags.Instance).ToList();

        var result = (IEnumerable<int>)_findDateTimeColumnsMethod.Invoke(null, [properties]);
        result.Should().BeEmpty();
    }

    #endregion

    #region Test Models

    private class TestItem
    {
        public string Name { get; set; }
        public NestedItem Nested { get; set; }
    }

    private class NestedItem
    {
        public string Value { get; set; }
    }

    private class DateTimeTestItem
    {
        public string Name { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTimeOffset Offset { get; set; }
        public DateTimeOffset? NullableOffset { get; set; }
    }

    private class NoDateItem
    {
        public string Name { get; set; }
        public int Count { get; set; }
    }

    #endregion
}
