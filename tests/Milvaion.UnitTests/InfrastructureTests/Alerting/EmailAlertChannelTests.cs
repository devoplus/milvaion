using FluentAssertions;
using Microsoft.Extensions.Options;
using Milvaion.Application.Dtos.AlertingDtos;
using Milvaion.Application.Utils.Models.Options;
using Milvaion.Domain.Enums;
using Milvaion.Infrastructure.Services.Alerting;
using Milvaion.Infrastructure.Services.Alerting.Channels;
using Milvasoft.Core.Abstractions;
using Moq;

namespace Milvaion.UnitTests.InfrastructureTests.Alerting;

[Trait("Infrastructure Unit Tests", "EmailAlertChannel unit tests.")]
public class EmailAlertChannelTests
{
    private static AlertPayload CreatePayload(AlertSeverity severity = AlertSeverity.Critical) => new()
    {
        Title = "Test Email Alert",
        Message = "Critical issue detected",
        Severity = severity,
        Source = "UnitTest",
        Timestamp = DateTime.UtcNow,
        ActionLink = "/jobs/456",
        AdditionalData = new { FailureCount = 5 }
    };

    private static AlertingOptions CreateEmailOptions(
        bool enabled = true,
        bool sendOnlyInProduction = false,
        string smtpHost = "smtp.test.com",
        string senderEmail = "alerts@test.com",
        List<string> recipients = null)
    => new()
    {
        SendOnlyInProduction = sendOnlyInProduction,
        Channels = new AlertChannelsOptions
        {
            Email = new EmailChannelOptions
            {
                Enabled = enabled,
                SendOnlyInProduction = sendOnlyInProduction,
                SmtpHost = smtpHost,
                SmtpPort = 587,
                UseSsl = true,
                SenderEmail = senderEmail,
                SenderPassword = "password",
                From = "noreply@test.com",
                DisplayName = "Milvaion Alerts",
                DefaultRecipients = recipients ?? ["admin@test.com"]
            }
        }
    };

    private static EmailAlertChannel CreateChannel(AlertingOptions options)
    {
        var loggerMock = new Mock<IMilvaLogger>();
        return new EmailAlertChannel(
            Options.Create(options),
            new Lazy<IMilvaLogger>(() => loggerMock.Object));
    }

    [Fact]
    public void ChannelName_ShouldBeEmail()
    {
        var channel = CreateChannel(CreateEmailOptions());
        channel.ChannelName.Should().Be("Email");
    }

    [Fact]
    public void IsEnabled_ShouldReturnTrue_WhenEnabled()
    {
        var channel = CreateChannel(CreateEmailOptions(enabled: true));
        channel.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_ShouldReturnFalse_WhenDisabled()
    {
        var channel = CreateChannel(CreateEmailOptions(enabled: false));
        channel.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void CanSend_ShouldReturnFalse_WhenDisabled()
    {
        var channel = CreateChannel(CreateEmailOptions(enabled: false));
        channel.CanSend().Should().BeFalse();
    }

    [Fact]
    public void CanSend_ShouldReturnFalse_WhenSmtpHostIsMissing()
    {
        var channel = CreateChannel(CreateEmailOptions(smtpHost: ""));
        channel.CanSend().Should().BeFalse();
    }

    [Fact]
    public void CanSend_ShouldReturnFalse_WhenSenderEmailIsMissing()
    {
        var channel = CreateChannel(CreateEmailOptions(senderEmail: ""));
        channel.CanSend().Should().BeFalse();
    }

    [Fact]
    public void CanSend_ShouldReturnFalse_WhenNoRecipients()
    {
        var channel = CreateChannel(CreateEmailOptions(recipients: []));
        channel.CanSend().Should().BeFalse();
    }

    [Fact]
    public void CanSend_ShouldReturnTrue_WhenFullyConfigured()
    {
        var channel = CreateChannel(CreateEmailOptions());
        channel.CanSend().Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_ShouldReturnSkipped_WhenDisabled()
    {
        var channel = CreateChannel(CreateEmailOptions(enabled: false));
        var result = await channel.SendAsync(AlertType.JobAutoDisabled, CreatePayload());

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("disabled");
    }

    [Fact]
    public async Task SendAsync_ShouldReturnSkipped_WhenSmtpHostMissing()
    {
        var channel = CreateChannel(CreateEmailOptions(smtpHost: ""));
        var result = await channel.SendAsync(AlertType.JobAutoDisabled, CreatePayload());

        result.Success.Should().BeTrue();
        result.Message.Should().Match(m => m.Contains("disabled") || m.Contains("environment"));
    }
}
