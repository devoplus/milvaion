using FluentAssertions;
using FluentValidation;
using Milvaion.Application.Behaviours;
using Milvaion.Application.Utils.Constants;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Types.Classes;
using Moq;

namespace Milvaion.UnitTests.BehavioursTests;

[Trait("Behaviours Unit Tests", "RuleBuilderOptionsExtensions unit tests.")]
public class RuleBuilderOptionsExtensionsTests
{
    private readonly Mock<IMilvaLocalizer> _mockLocalizer;

    public RuleBuilderOptionsExtensionsTests()
    {
        _mockLocalizer = new Mock<IMilvaLocalizer>();
        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedValue(key, key));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string key, object[] args) => new LocalizedValue(key, key));
    }

    #region NotBeDefaultData (int)

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(21, true)]
    [InlineData(100, true)]
    [InlineData(1, false)]
    [InlineData(10, false)]
    [InlineData(20, false)]
    public void NotBeDefaultData_IntValue_ShouldValidateCorrectly(int value, bool expectedValid)
    {
        // Arrange
        var validator = new InlineValidator<TestIntModel>();
        validator.RuleFor(x => x.Value).NotBeDefaultData();
        var model = new TestIntModel { Value = value };

        // Act
        var result = validator.Validate(model);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    [Theory]
    [InlineData(5, 10, 3, true)]
    [InlineData(5, 10, 5, true)]
    [InlineData(5, 10, 10, true)]
    [InlineData(5, 10, 15, true)]
    [InlineData(5, 10, 6, false)]
    [InlineData(5, 10, 9, false)]
    public void NotBeDefaultData_IntValueWithCustomRange_ShouldValidateCorrectly(int rangeMin, int rangeMax, int value, bool expectedValid)
    {
        // Arrange
        var validator = new InlineValidator<TestIntModel>();
        validator.RuleFor(x => x.Value).NotBeDefaultData(rangeMin, rangeMax);
        var model = new TestIntModel { Value = value };

        // Act
        var result = validator.Validate(model);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    #endregion

    #region NotBeDefaultData (long)

    [Theory]
    [InlineData(0L, true)]
    [InlineData(-1L, true)]
    [InlineData(21L, true)]
    [InlineData(100L, true)]
    [InlineData(1L, false)]
    [InlineData(10L, false)]
    [InlineData(20L, false)]
    public void NotBeDefaultData_LongValue_ShouldValidateCorrectly(long value, bool expectedValid)
    {
        // Arrange
        var validator = new InlineValidator<TestLongModel>();
        validator.RuleFor(x => x.Value).NotBeDefaultData();
        var model = new TestLongModel { Value = value };

        // Act
        var result = validator.Validate(model);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    #endregion

    #region Email

    [Theory]
    [InlineData("test@example.com", true)]
    [InlineData("user@domain.org", true)]
    [InlineData("invalid-email", false)]
    [InlineData("", false)]
    public void Email_ShouldValidateCorrectly(string email, bool expectedValid)
    {
        // Arrange
        var validator = new InlineValidator<TestStringModel>();
        validator.RuleFor(x => x.Value).Email(_mockLocalizer.Object);
        var model = new TestStringModel { Value = email };

        // Act
        var result = validator.Validate(model);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    #endregion

    #region PhoneNumber

    [Theory]
    [InlineData("+905551234567", true)]
    [InlineData("+901234567890", true)]
    [InlineData("05551234567", false)]
    [InlineData("+90555123456", false)]
    [InlineData("+9055512345678", false)]
    [InlineData("", false)]
    public void PhoneNumber_ShouldValidateCorrectly(string phoneNumber, bool expectedValid)
    {
        // Arrange
        var validator = new InlineValidator<TestStringModel>();
        validator.RuleFor(x => x.Value).PhoneNumber(_mockLocalizer.Object);
        var model = new TestStringModel { Value = phoneNumber };

        // Act
        var result = validator.Validate(model);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    #endregion

    #region UrlAddress

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("http://example.com", true)]
    [InlineData("example.com", true)]
    [InlineData("https://example.com/path", true)]
    [InlineData("", false)]
    public void UrlAddress_ShouldValidateCorrectly(string url, bool expectedValid)
    {
        // Arrange
        var validator = new InlineValidator<TestStringModel>();
        validator.RuleFor(x => x.Value).UrlAddress(_mockLocalizer.Object);
        var model = new TestStringModel { Value = url };

        // Act
        var result = validator.Validate(model);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    #endregion

    #region NotNullOrEmpty (string)

    [Theory]
    [InlineData("valid", true)]
    [InlineData("a", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void NotNullOrEmpty_String_ShouldValidateCorrectly(string value, bool expectedValid)
    {
        // Arrange
        var validator = new InlineValidator<TestStringModel>();
        validator.RuleFor(x => x.Value).NotNullOrEmpty(_mockLocalizer.Object, MessageKey.GlobalName);
        var model = new TestStringModel { Value = value };

        // Act
        var result = validator.Validate(model);

        // Assert
        result.IsValid.Should().Be(expectedValid);
    }

    #endregion

    #region Test Models

    public class TestIntModel
    {
        public int Value { get; set; }
    }

    public class TestLongModel
    {
        public long Value { get; set; }
    }

    public class TestStringModel
    {
        public string Value { get; set; }
    }

    #endregion
}
