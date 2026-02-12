using FluentAssertions;
using Milvaion.Application.Utils.Extensions;

namespace Milvaion.UnitTests.ApplicationTests;

[Trait("Application Unit Tests", "MilvaionExtensions unit tests.")]
public class MilvaionExtensionsTests
{
    #region IsBase64StringValidLength

    [Fact]
    public void IsBase64StringValidLength_ShouldReturnTrue_WhenEmpty() => MilvaionExtensions.IsBase64StringValidLength("").Should().BeTrue();

    [Fact]
    public void IsBase64StringValidLength_ShouldReturnTrue_WhenNull() => MilvaionExtensions.IsBase64StringValidLength(null).Should().BeTrue();

    [Fact]
    public void IsBase64StringValidLength_ShouldReturnTrue_WhenSmallBase64() => MilvaionExtensions.IsBase64StringValidLength("SGVsbG8=").Should().BeTrue();

    [Fact]
    public void IsBase64StringValidLength_ShouldReturnFalse_WhenLargerThan1MB()
    {
        // Create a base64 string that decodes to > 1MB
        var largeData = new string('A', 1_500_000); // ~1.07MB decoded
        MilvaionExtensions.IsBase64StringValidLength(largeData).Should().BeFalse();
    }

    [Fact]
    public void IsBase64StringValidLength_ShouldHandleDoublePaddedBase64() => MilvaionExtensions.IsBase64StringValidLength("SA==").Should().BeTrue();

    [Fact]
    public void IsBase64StringValidLength_ShouldHandleSinglePaddedBase64() => MilvaionExtensions.IsBase64StringValidLength("SGU=").Should().BeTrue();

    [Fact]
    public void IsBase64StringValidLength_ShouldHandleNoPaddingBase64() => MilvaionExtensions.IsBase64StringValidLength("SGVs").Should().BeTrue();

    #endregion

    #region IsBase64StringHasValidFileExtension

    [Fact]
    public void IsBase64StringHasValidFileExtension_ShouldReturnTrue_WhenEmpty() => MilvaionExtensions.IsBase64StringHasValidFileExtension("").Should().BeTrue();

    [Fact]
    public void IsBase64StringHasValidFileExtension_ShouldReturnTrue_WhenNull() => MilvaionExtensions.IsBase64StringHasValidFileExtension(null).Should().BeTrue();

    [Fact]
    public void IsBase64StringHasValidFileExtension_ShouldReturnTrue_ForJpegDataUri()
    {
        var dataUri = "data:image/jpeg;base64,/9j/4AAQSkZJRg==";
        MilvaionExtensions.IsBase64StringHasValidFileExtension(dataUri).Should().BeTrue();
    }

    [Fact]
    public void IsBase64StringHasValidFileExtension_ShouldReturnTrue_ForPngDataUri()
    {
        var dataUri = "data:image/png;base64,iVBORw0KGgo=";
        MilvaionExtensions.IsBase64StringHasValidFileExtension(dataUri).Should().BeTrue();
    }

    [Fact]
    public void IsBase64StringHasValidFileExtension_ShouldReturnFalse_ForUnsupportedMimeType()
    {
        var dataUri = "data:application/pdf;base64,JVBERi0=";
        MilvaionExtensions.IsBase64StringHasValidFileExtension(dataUri).Should().BeFalse();
    }

    [Fact]
    public void IsBase64StringHasValidFileExtension_ShouldReturnFalse_WhenNoDataPrefix() => MilvaionExtensions.IsBase64StringHasValidFileExtension("SGVsbG8=").Should().BeFalse();

    #endregion

    #region IsValidDataUri

    [Fact]
    public void IsValidDataUri_ShouldReturnTrue_ForValidJpegDataUri()
    {
        var dataUri = "data:image/jpeg;base64,/9j/4AAQSkZJRg==";
        MilvaionExtensions.IsValidDataUri(dataUri).Should().BeTrue();
    }

    [Fact]
    public void IsValidDataUri_ShouldReturnFalse_ForInvalidFormat() => MilvaionExtensions.IsValidDataUri("not-a-data-uri").Should().BeFalse();

    [Fact]
    public void IsValidDataUri_ShouldReturnFalse_ForEmptyString() => MilvaionExtensions.IsValidDataUri("").Should().BeFalse();

    #endregion

    #region DataUriToPlainText

    [Fact]
    public void DataUriToPlainText_ShouldReturnEmpty_WhenNull() => MilvaionExtensions.DataUriToPlainText(null).Should().BeEmpty();

    [Fact]
    public void DataUriToPlainText_ShouldReturnEmpty_WhenEmpty() => MilvaionExtensions.DataUriToPlainText("").Should().BeEmpty();

    [Fact]
    public void DataUriToPlainText_ShouldReturnEmpty_WhenInvalidFormat() => MilvaionExtensions.DataUriToPlainText("no-base64-marker").Should().BeEmpty();

    [Fact]
    public void DataUriToPlainText_ShouldDecodeValidDataUri()
    {
        // "Hello" = SGVsbG8=
        var dataUri = "data:text/plain;base64,SGVsbG8=";
        var result = MilvaionExtensions.DataUriToPlainText(dataUri);
        result.Should().NotBeEmpty();
        System.Text.Encoding.UTF8.GetString(result).Should().Be("Hello");
    }

    #endregion

    #region GetCurrentEnvironment / IsCurrentEnvProduction

    [Fact]
    public void IsCurrentEnvProduction_ShouldReturnFalse_InTestEnvironment() => MilvaionExtensions.IsCurrentEnvProduction().Should().BeFalse();

    [Fact]
    public void GetCurrentEnvironment_ShouldReturnString()
    {
        var result = MilvaionExtensions.GetCurrentEnvironment();
        result.Should().NotBeNull();
    }

    #endregion

    #region UrlRegex

    [Fact]
    public void UrlRegex_ShouldMatchValidHttpUrl() => MilvaionExtensions.UrlRegex().IsMatch("http://example.com").Should().BeTrue();

    [Fact]
    public void UrlRegex_ShouldMatchValidHttpsUrl() => MilvaionExtensions.UrlRegex().IsMatch("https://example.com/path").Should().BeTrue();

    [Fact]
    public void UrlRegex_ShouldNotMatchInvalidUrl() => MilvaionExtensions.UrlRegex().IsMatch("not-a-url").Should().BeFalse();

    #endregion
}
