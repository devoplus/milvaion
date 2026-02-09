using FluentAssertions;
using FluentValidation;
using Milvaion.Application.Behaviours;
using Milvaion.Application.Dtos.TranslationDtos;
using Milvasoft.Core.Abstractions.Localization;
using Milvasoft.Types.Classes;
using Milvasoft.Types.Structs;
using Moq;

namespace Milvaion.UnitTests.BehavioursTests;

#pragma warning disable IDE0022 // Use expression body for method
[Trait("Behaviours Unit Tests", "CustomValidators unit tests.")]
public class CustomValidatorsTests
{
    private readonly Mock<IMilvaLocalizer> _mockLocalizer;

    public CustomValidatorsTests()
    {
        _mockLocalizer = new Mock<IMilvaLocalizer>();
        _mockLocalizer.Setup(l => l[It.IsAny<string>()]).Returns((string key) => new LocalizedValue(key, key));
        _mockLocalizer.Setup(l => l[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string key, object[] args) => new LocalizedValue(key, key));
    }

    #region DefaultDataValidator

    [Fact]
    public void DefaultDataValidator_Name_ShouldReturnDefaultDataValidator()
    {
        // Arrange
        var validator = new DefaultDataValidator<object>();

        // Assert
        validator.Name.Should().Be("DefaultDataValidator");
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(-100, true)]
    [InlineData(21, true)]
    [InlineData(100, true)]
    [InlineData(1, false)]
    [InlineData(10, false)]
    [InlineData(20, false)]
    public void DefaultDataValidator_IsValid_WithDefaultRange_ShouldReturnExpectedResult(int value, bool expected)
    {
        // Arrange
        var validator = new DefaultDataValidator<object>();
        var context = new ValidationContext<object>(new object());

        // Act
        var result = validator.IsValid(context, value);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(5, 10, 5, true)]
    [InlineData(5, 10, 3, true)]
    [InlineData(5, 10, 10, true)]
    [InlineData(5, 10, 15, true)]
    [InlineData(5, 10, 6, false)]
    [InlineData(5, 10, 9, false)]
    public void DefaultDataValidator_IsValid_WithCustomRange_ShouldReturnExpectedResult(int rangeMin, int rangeMax, int value, bool expected)
    {
        // Arrange
        var validator = new DefaultDataValidator<object>(rangeMin, rangeMax);
        var context = new ValidationContext<object>(new object());

        // Act
        var result = validator.IsValid(context, value);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region DefaultDataLongValidator

    [Fact]
    public void DefaultDataLongValidator_Name_ShouldReturnDefaultDataValidator()
    {
        // Arrange
        var validator = new DefaultDataLongValidator<object>();

        // Assert
        validator.Name.Should().Be("DefaultDataValidator");
    }

    [Theory]
    [InlineData(0L, true)]
    [InlineData(-1L, true)]
    [InlineData(-100L, true)]
    [InlineData(21L, true)]
    [InlineData(100L, true)]
    [InlineData(1L, false)]
    [InlineData(10L, false)]
    [InlineData(20L, false)]
    public void DefaultDataLongValidator_IsValid_WithDefaultRange_ShouldReturnExpectedResult(long value, bool expected)
    {
        // Arrange
        var validator = new DefaultDataLongValidator<object>();
        var context = new ValidationContext<object>(new object());

        // Act
        var result = validator.IsValid(context, value);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(5L, 50L, 5L, true)]
    [InlineData(5L, 50L, 3L, true)]
    [InlineData(5L, 50L, 50L, true)]
    [InlineData(5L, 50L, 100L, true)]
    [InlineData(5L, 50L, 6L, false)]
    [InlineData(5L, 50L, 49L, false)]
    public void DefaultDataLongValidator_IsValid_WithCustomRange_ShouldReturnExpectedResult(long rangeMin, long rangeMax, long value, bool expected)
    {
        // Arrange
        var validator = new DefaultDataLongValidator<object>(rangeMin, rangeMax);
        var context = new ValidationContext<object>(new object());

        // Act
        var result = validator.IsValid(context, value);

        // Assert
        result.Should().Be(expected);
    }

    #endregion

    #region NameDescriptionTranslationDtoValidator

    [Fact]
    public void NameDescriptionTranslationDtoValidator_ValidDto_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var validator = new NameDescriptionTranslationDtoValidator(_mockLocalizer.Object);
        var dto = new NameDescriptionTranslationDto
        {
            Name = "ValidName",
            Description = "ValidDescription",
            LanguageId = 22
        };

        // Act
        var result = validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void NameDescriptionTranslationDtoValidator_EmptyName_ShouldHaveValidationError()
    {
        // Arrange
        var validator = new NameDescriptionTranslationDtoValidator(_mockLocalizer.Object);
        var dto = new NameDescriptionTranslationDto
        {
            Name = "",
            Description = "ValidDescription",
            LanguageId = 22
        };

        // Act
        var result = validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void NameDescriptionTranslationDtoValidator_NullName_ShouldHaveValidationError()
    {
        // Arrange
        var validator = new NameDescriptionTranslationDtoValidator(_mockLocalizer.Object);
        var dto = new NameDescriptionTranslationDto
        {
            Name = null,
            Description = "ValidDescription",
            LanguageId = 22
        };

        // Act
        var result = validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void NameDescriptionTranslationDtoValidator_NameExceedsMaxLength_ShouldHaveValidationError()
    {
        // Arrange
        var validator = new NameDescriptionTranslationDtoValidator(_mockLocalizer.Object);
        var dto = new NameDescriptionTranslationDto
        {
            Name = new string('a', 101),
            Description = "ValidDescription",
            LanguageId = 22
        };

        // Act
        var result = validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Name");
    }

    [Fact]
    public void NameDescriptionTranslationDtoValidator_DescriptionExceedsMaxLength_ShouldHaveValidationError()
    {
        // Arrange
        var validator = new NameDescriptionTranslationDtoValidator(_mockLocalizer.Object);
        var dto = new NameDescriptionTranslationDto
        {
            Name = "ValidName",
            Description = new string('a', 5001),
            LanguageId = 22
        };

        // Act
        var result = validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Description");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1)]
    [InlineData(20)]
    public void NameDescriptionTranslationDtoValidator_InvalidLanguageId_ShouldHaveValidationError(int languageId)
    {
        // Arrange
        var validator = new NameDescriptionTranslationDtoValidator(_mockLocalizer.Object);
        var dto = new NameDescriptionTranslationDto
        {
            Name = "ValidName",
            Description = "ValidDescription",
            LanguageId = languageId
        };

        // Act
        var result = validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "LanguageId");
    }

    [Fact]
    public void NameDescriptionTranslationDtoValidator_NullDescription_ShouldNotHaveValidationError()
    {
        // Arrange
        var validator = new NameDescriptionTranslationDtoValidator(_mockLocalizer.Object);
        var dto = new NameDescriptionTranslationDto
        {
            Name = "ValidName",
            Description = null,
            LanguageId = 22
        };

        // Act
        var result = validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion

    #region UpdateNameDescriptionTranslationDtoValidator

    [Fact]
    public void UpdateNameDescriptionTranslationDtoValidator_IsNotUpdated_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var validator = new UpdateNameDescriptionTranslationDtoValidator(_mockLocalizer.Object);
        var dto = new UpdateProperty<List<NameDescriptionTranslationDto>>
        {
            IsUpdated = false,
            Value = null
        };

        // Act
        var result = validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateNameDescriptionTranslationDtoValidator_IsUpdatedWithValidData_ShouldNotHaveValidationErrors()
    {
        // Arrange
        var validator = new UpdateNameDescriptionTranslationDtoValidator(_mockLocalizer.Object);
        var dto = new UpdateProperty<List<NameDescriptionTranslationDto>>
        {
            IsUpdated = true,
            Value =
            [
                new NameDescriptionTranslationDto
                {
                    Name = "ValidName",
                    Description = "ValidDescription",
                    LanguageId = 22
                }
            ]
        };

        // Act
        var result = validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UpdateNameDescriptionTranslationDtoValidator_IsUpdatedWithInvalidChildData_ShouldHaveValidationErrors()
    {
        // Arrange
        var validator = new UpdateNameDescriptionTranslationDtoValidator(_mockLocalizer.Object);
        var dto = new UpdateProperty<List<NameDescriptionTranslationDto>>
        {
            IsUpdated = true,
            Value =
            [
                new NameDescriptionTranslationDto
                {
                    Name = "",
                    Description = "ValidDescription",
                    LanguageId = 0
                }
            ]
        };

        // Act
        var result = validator.Validate(dto);

        // Assert
        result.IsValid.Should().BeFalse();
    }

    #endregion
}
