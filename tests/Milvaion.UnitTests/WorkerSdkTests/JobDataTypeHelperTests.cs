using FluentAssertions;
using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using Milvasoft.Milvaion.Sdk.Worker.Utils;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Milvaion.UnitTests.WorkerSdkTests;

[Trait("SDK Unit Tests", "JobDataTypeHelper unit tests.")]
public class JobDataTypeHelperTests
{
    #region GetJobDataType

    [Fact]
    public void GetJobDataType_ShouldReturnNull_ForNonGenericJob()
    {
        // Act
        var result = JobDataTypeHelper.GetJobDataType(typeof(SimpleJob));

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GetJobDataType_ShouldReturnType_ForGenericIJob()
    {
        // Act
        var result = JobDataTypeHelper.GetJobDataType(typeof(TypedJob));

        // Assert
        result.Should().Be<SampleJobData>();
    }

    [Fact]
    public void GetJobDataType_ShouldReturnType_ForGenericIAsyncJob()
    {
        // Act
        var result = JobDataTypeHelper.GetJobDataType(typeof(AsyncTypedJob));

        // Assert
        result.Should().Be<SampleJobData>();
    }

    #endregion

    #region GenerateSchema

    [Fact]
    public void GenerateSchema_ShouldReturnNull_WhenTypeIsNull()
    {
        // Act
        var result = JobDataTypeHelper.GenerateSchema(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GenerateSchema_ShouldGenerateValidSchema()
    {
        // Act
        var schema = JobDataTypeHelper.GenerateSchema(typeof(SampleJobData));

        // Assert
        schema.Should().NotBeNull();
        schema.Should().ContainKey("$schema");
        schema.Should().ContainKey("type");
        schema["type"].Should().Be("object");
        schema.Should().ContainKey("title");
        schema["title"].Should().Be(nameof(SampleJobData));
        schema.Should().ContainKey("properties");
    }

    [Fact]
    public void GenerateSchema_ShouldIncludeStringProperty()
    {
        // Act
        var schema = JobDataTypeHelper.GenerateSchema(typeof(SampleJobData));
        var properties = (Dictionary<string, object>)schema["properties"];

        // Assert
        properties.Should().ContainKey("recipient");
        var recipientSchema = (Dictionary<string, object>)properties["recipient"];
        recipientSchema["type"].Should().Be("string");
    }

    [Fact]
    public void GenerateSchema_ShouldIncludeIntegerProperty()
    {
        // Act
        var schema = JobDataTypeHelper.GenerateSchema(typeof(SampleJobData));
        var properties = (Dictionary<string, object>)schema["properties"];

        // Assert
        properties.Should().ContainKey("retryCount");
        var retrySchema = (Dictionary<string, object>)properties["retryCount"];
        retrySchema["type"].Should().Be("integer");
    }

    [Fact]
    public void GenerateSchema_ShouldIncludeBooleanProperty()
    {
        // Act
        var schema = JobDataTypeHelper.GenerateSchema(typeof(SampleJobData));
        var properties = (Dictionary<string, object>)schema["properties"];

        // Assert
        properties.Should().ContainKey("sendNotification");
        var boolSchema = (Dictionary<string, object>)properties["sendNotification"];
        boolSchema["type"].Should().Be("boolean");
    }

    [Fact]
    public void GenerateSchema_ShouldIncludeRequiredProperties()
    {
        // Act
        var schema = JobDataTypeHelper.GenerateSchema(typeof(SampleJobData));
        var required = (List<string>)schema["required"];

        // Assert
        required.Should().Contain("recipient");
    }

    [Fact]
    public void GenerateSchema_ShouldIncludeDescription_WhenDescriptionAttributePresent()
    {
        // Act
        var schema = JobDataTypeHelper.GenerateSchema(typeof(SampleJobData));
        var properties = (Dictionary<string, object>)schema["properties"];
        var recipientSchema = (Dictionary<string, object>)properties["recipient"];

        // Assert
        recipientSchema.Should().ContainKey("description");
        recipientSchema["description"].Should().Be("Email recipient");
    }

    #endregion

    #region GenerateSchemaJson

    [Fact]
    public void GenerateSchemaJson_ShouldReturnNull_WhenTypeIsNull()
    {
        // Act
        var result = JobDataTypeHelper.GenerateSchemaJson(null);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GenerateSchemaJson_ShouldReturnValidJsonString()
    {
        // Act
        var json = JobDataTypeHelper.GenerateSchemaJson(typeof(SampleJobData));

        // Assert
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("\"type\":\"object\"");
        json.Should().Contain("\"recipient\"");
    }

    #endregion

    #region GetJobDataInfo

    [Fact]
    public void GetJobDataInfo_ShouldReturnNull_ForNonGenericJob()
    {
        // Act
        var info = JobDataTypeHelper.GetJobDataInfo(typeof(SimpleJob));

        // Assert
        info.Should().BeNull();
    }

    [Fact]
    public void GetJobDataInfo_ShouldReturnInfo_ForTypedJob()
    {
        // Act
        var info = JobDataTypeHelper.GetJobDataInfo(typeof(TypedJob));

        // Assert
        info.Should().NotBeNull();
        info.TypeShortName.Should().Be(nameof(SampleJobData));
        info.SchemaJson.Should().NotBeNullOrWhiteSpace();
        info.Schema.Should().NotBeNull();
    }

    #endregion

    #region DynamicEnum

    [Fact]
    public void RegisterDynamicEnumValues_ShouldStoreValues()
    {
        // Arrange
        var key = "TestConfig:DynamicValues";
        var values = new[] { "Value1", "Value2", "Value3" };

        // Act
        JobDataTypeHelper.RegisterDynamicEnumValues(key, values);
        var result = JobDataTypeHelper.GetDynamicEnumValues(key);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain("Value1");
        result.Should().Contain("Value2");
        result.Should().Contain("Value3");
    }

    [Fact]
    public void GetDynamicEnumValues_ShouldReturnEmpty_WhenKeyNotRegistered()
    {
        // Act
        var result = JobDataTypeHelper.GetDynamicEnumValues("NonExistent:Key");

        // Assert
        result.Should().BeEmpty();
    }

    #endregion

    #region Test Types

    private sealed class SimpleJob : IAsyncJob
    {
        public Task ExecuteAsync(IJobContext context) => Task.CompletedTask;
    }

    private sealed class TypedJob : IJob<SampleJobData>
    {
        public void Execute(IJobContext context) { }
    }

    private sealed class AsyncTypedJob : IAsyncJob<SampleJobData>
    {
        public Task ExecuteAsync(IJobContext context) => Task.CompletedTask;
    }

    public class SampleJobData
    {
        [Required]
        [Description("Email recipient")]
        public string Recipient { get; set; }

        public int RetryCount { get; set; }

        public bool SendNotification { get; set; }
    }

    #endregion
}
