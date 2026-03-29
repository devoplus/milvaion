using FluentAssertions;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Milvaion.Infrastructure.Utils.OpenApi;
using Milvasoft.Components.Rest.Request;
using Milvasoft.DataAccess.EfCore.Utils.LookupModels;
using System.Text.Json.Serialization.Metadata;

namespace Milvaion.UnitTests.InfrastructureTests;

[Trait("Infrastructure Unit Tests", "ExampleSchemaTransformer unit tests.")]
public class ExampleSchemaTransformerTests
{
    private readonly ExampleSchemaTransformer _transformer = new();

    [Fact]
    public async Task TransformAsync_WithListRequestType_ShouldSetExample()
    {
        // Arrange
        var schema = new OpenApiSchema();
        var context = CreateContext(typeof(ListRequest));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Example.Should().NotBeNull("ListRequest type should produce an example");
    }

    [Fact]
    public async Task TransformAsync_WithListRequestSubclass_ShouldSetExample()
    {
        // Arrange
        var schema = new OpenApiSchema();
        var context = CreateContext(typeof(TestListRequest));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Example.Should().NotBeNull("subclass of ListRequest should also produce an example");
    }

    [Fact]
    public async Task TransformAsync_WithLookupRequestType_ShouldSetExample()
    {
        // Arrange
        var schema = new OpenApiSchema();
        var context = CreateContext(typeof(LookupRequest));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Example.Should().NotBeNull("LookupRequest type should produce an example");
    }

    [Fact]
    public async Task TransformAsync_WithUnrelatedType_ShouldNotSetExample()
    {
        // Arrange
        var schema = new OpenApiSchema();
        var context = CreateContext(typeof(string));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Example.Should().BeNull("unrelated types should not produce examples");
    }

    [Fact]
    public async Task TransformAsync_WithObjectType_ShouldNotSetExample()
    {
        // Arrange
        var schema = new OpenApiSchema();
        var context = CreateContext(typeof(object));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Example.Should().BeNull();
    }

    [Fact]
    public async Task TransformAsync_WithIntType_ShouldNotSetExample()
    {
        // Arrange
        var schema = new OpenApiSchema();
        var context = CreateContext(typeof(int));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        schema.Example.Should().BeNull();
    }

    [Fact]
    public async Task TransformAsync_ListRequestExample_ShouldContainExpectedFields()
    {
        // Arrange
        var schema = new OpenApiSchema();
        var context = CreateContext(typeof(ListRequest));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        var json = schema.Example.ToString();
        json.Should().Contain("pageNumber");
        json.Should().Contain("rowCount");
        json.Should().Contain("filtering");
        json.Should().Contain("sorting");
        json.Should().Contain("aggregation");
    }

    [Fact]
    public async Task TransformAsync_LookupRequestExample_ShouldContainExpectedFields()
    {
        // Arrange
        var schema = new OpenApiSchema();
        var context = CreateContext(typeof(LookupRequest));

        // Act
        await _transformer.TransformAsync(schema, context, CancellationToken.None);

        // Assert
        var json = schema.Example.ToString();
        json.Should().Contain("parameters");
        json.Should().Contain("entityName");
        json.Should().Contain("requestedPropertyNames");
    }

    private static OpenApiSchemaTransformerContext CreateContext(Type type)
    {
        var jsonTypeInfo = JsonTypeInfo.CreateJsonTypeInfo(type, new System.Text.Json.JsonSerializerOptions());

        return new OpenApiSchemaTransformerContext
        {
            DocumentName = "v1",
            JsonTypeInfo = jsonTypeInfo,
            JsonPropertyInfo = null,
            ParameterDescription = null,
            ApplicationServices = null,
        };
    }

    private record TestListRequest : ListRequest { }
}
