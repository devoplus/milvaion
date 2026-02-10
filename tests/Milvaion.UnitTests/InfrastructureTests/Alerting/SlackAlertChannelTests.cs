using FluentAssertions;
using Microsoft.Extensions.Options;
using Milvaion.Application.Dtos.AlertingDtos;
using Milvaion.Application.Utils.Models.Options;
using Milvaion.Domain.Enums;
using Milvaion.Infrastructure.Services.Alerting.Channels;
using Milvasoft.Core.Abstractions;
using Moq;
using Moq.Protected;
using System.Net;

namespace Milvaion.UnitTests.InfrastructureTests.Alerting;

[Trait("Infrastructure Unit Tests", "SlackAlertChannel unit tests.")]
public class SlackAlertChannelTests
{
    private static AlertPayload CreatePayload(AlertSeverity severity = AlertSeverity.Warning) => new()
    {
        Title = "Test Slack Alert",
        Message = "Something happened",
        Severity = severity,
        Source = "UnitTest",
        Timestamp = DateTime.UtcNow,
        AdditionalData = new { Key = "value" }
    };

    private static AlertingOptions CreateSlackOptions(bool enabled = true, bool sendOnlyInProduction = false)
    => new()
    {
        SendOnlyInProduction = sendOnlyInProduction,
        Channels = new AlertChannelsOptions
        {
            Slack = new SlackChannelOptions
            {
                Enabled = enabled,
                SendOnlyInProduction = sendOnlyInProduction,
                DefaultChannel = "alerts",
                Channels =
                [
                    new SlackChannelConfig
                    {
                        Channel = "alerts",
                        WebhookUrl = "https://hooks.slack.com/services/fake/webhook"
                    }
                ]
            }
        }
    };

    private static (SlackAlertChannel Channel, Mock<HttpMessageHandler> Handler) CreateChannel(AlertingOptions options, HttpStatusCode responseStatus = HttpStatusCode.OK, string responseBody = "ok")
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = responseStatus,
                Content = new StringContent(responseBody)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient(nameof(SlackAlertChannel))).Returns(httpClient);

        var loggerMock = new Mock<IMilvaLogger>();
        var channel = new SlackAlertChannel(
            Options.Create(options),
            httpClientFactoryMock.Object,
            new Lazy<IMilvaLogger>(() => loggerMock.Object));

        return (channel, handlerMock);
    }

    [Fact]
    public void ChannelName_ShouldBeSlack()
    {
        // Arrange
        var (channel, _) = CreateChannel(CreateSlackOptions());

        // Assert
        channel.ChannelName.Should().Be("Slack");
    }

    [Fact]
    public void IsEnabled_ShouldReturnTrue_WhenEnabled()
    {
        var (channel, _) = CreateChannel(CreateSlackOptions(enabled: true));
        channel.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_ShouldReturnFalse_WhenDisabled()
    {
        var (channel, _) = CreateChannel(CreateSlackOptions(enabled: false));
        channel.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_ShouldReturnSkipped_WhenChannelIsDisabled()
    {
        // Arrange
        var (channel, _) = CreateChannel(CreateSlackOptions(enabled: false));

        // Act
        var result = await channel.SendAsync(AlertType.JobAutoDisabled, CreatePayload());

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("disabled");
    }

    [Fact]
    public async Task SendAsync_ShouldReturnSuccess_WhenWebhookReturnsOk()
    {
        // Arrange
        var (channel, handler) = CreateChannel(CreateSlackOptions());

        // Act
        var result = await channel.SendAsync(AlertType.JobAutoDisabled, CreatePayload());

        // Assert
        result.Success.Should().BeTrue();
        result.ChannelName.Should().Be("Slack");

        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_ShouldReturnFailed_WhenWebhookReturnsError()
    {
        // Arrange
        var (channel, _) = CreateChannel(CreateSlackOptions(), HttpStatusCode.InternalServerError, "server_error1");

        // Act
        var result = await channel.SendAsync(AlertType.DatabaseConnectionFailed, CreatePayload());

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("server_error1");
    }

    [Fact]
    public async Task SendAsync_ShouldPostToConfiguredWebhookUrl()
    {
        // Arrange
        Uri capturedUri = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedUri = req.RequestUri)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });

        var httpClient = new HttpClient(handlerMock.Object);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient(nameof(SlackAlertChannel))).Returns(httpClient);

        var loggerMock = new Mock<IMilvaLogger>();
        var channel = new SlackAlertChannel(
            Options.Create(CreateSlackOptions()),
            httpClientFactoryMock.Object,
            new Lazy<IMilvaLogger>(() => loggerMock.Object));

        // Act
        await channel.SendAsync(AlertType.WorkerDisconnected, CreatePayload());

        // Assert
        capturedUri.Should().NotBeNull();
        capturedUri!.ToString().Should().Be("https://hooks.slack.com/services/fake/webhook");
    }

    [Fact]
    public async Task SendAsync_ShouldReturnSkipped_WhenNoWebhookConfigured()
    {
        // Arrange
        var options = new AlertingOptions
        {
            SendOnlyInProduction = false,
            Channels = new AlertChannelsOptions
            {
                Slack = new SlackChannelOptions
                {
                    Enabled = true,
                    SendOnlyInProduction = false,
                    DefaultChannel = "nonexistent",
                    Channels = []
                }
            }
        };
        var (channel, _) = CreateChannel(options);

        // Act
        var result = await channel.SendAsync(AlertType.JobAutoDisabled, CreatePayload());

        // Assert
        result.Message.Should().Contain("No webhook");
    }

    [Fact]
    public async Task SendAsync_ShouldIncludeAllSeverityLevels()
    {
        foreach (var severity in Enum.GetValues<AlertSeverity>())
        {
            var (channel, _) = CreateChannel(CreateSlackOptions());
            var result = await channel.SendAsync(AlertType.UnknownException, CreatePayload(severity));
            result.Success.Should().BeTrue($"should succeed for severity {severity}");
        }
    }
}
