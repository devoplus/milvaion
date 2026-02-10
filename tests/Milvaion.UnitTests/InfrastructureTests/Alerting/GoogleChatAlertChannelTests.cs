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

[Trait("Infrastructure Unit Tests", "GoogleChatAlertChannel unit tests.")]
public class GoogleChatAlertChannelTests
{
    private static AlertPayload CreatePayload(AlertSeverity severity = AlertSeverity.Error) => new()
    {
        Title = "Test Google Chat Alert",
        Message = "Something critical happened",
        Severity = severity,
        Source = "UnitTest",
        Timestamp = DateTime.UtcNow,
        AdditionalData = new { JobId = "abc-123" }
    };

    private static AlertingOptions CreateGoogleChatOptions(bool enabled = true, bool sendOnlyInProduction = false)
    => new()
    {
        SendOnlyInProduction = sendOnlyInProduction,
        Channels = new AlertChannelsOptions
        {
            GoogleChat = new GoogleChatChannelOptions
            {
                Enabled = enabled,
                SendOnlyInProduction = sendOnlyInProduction,
                DefaultSpace = "alerts",
                Spaces =
                [
                    new GoogleChatSpaceConfig
                    {
                        Space = "alerts",
                        WebhookUrl = "https://chat.googleapis.com/v1/spaces/FAKE/messages?key=fakekey&token=faketoken"
                    }
                ]
            }
        }
    };

    private static (GoogleChatAlertChannel Channel, Mock<HttpMessageHandler> Handler) CreateChannel(AlertingOptions options, HttpStatusCode responseStatus = HttpStatusCode.OK, string responseBody = "{}")
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
        httpClientFactoryMock.Setup(f => f.CreateClient(nameof(GoogleChatAlertChannel))).Returns(httpClient);

        var loggerMock = new Mock<IMilvaLogger>();
        var channel = new GoogleChatAlertChannel(
            Options.Create(options),
            httpClientFactoryMock.Object,
            new Lazy<IMilvaLogger>(() => loggerMock.Object));

        return (channel, handlerMock);
    }

    [Fact]
    public void ChannelName_ShouldBeGoogleChat()
    {
        var (channel, _) = CreateChannel(CreateGoogleChatOptions());
        channel.ChannelName.Should().Be("GoogleChat");
    }

    [Fact]
    public void IsEnabled_ShouldReturnTrue_WhenEnabled()
    {
        var (channel, _) = CreateChannel(CreateGoogleChatOptions(enabled: true));
        channel.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_ShouldReturnFalse_WhenDisabled()
    {
        var (channel, _) = CreateChannel(CreateGoogleChatOptions(enabled: false));
        channel.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_ShouldReturnSkipped_WhenDisabled()
    {
        var (channel, _) = CreateChannel(CreateGoogleChatOptions(enabled: false));
        var result = await channel.SendAsync(AlertType.JobAutoDisabled, CreatePayload());
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("disabled");
    }

    [Fact]
    public async Task SendAsync_ShouldReturnSuccess_WhenWebhookReturnsOk()
    {
        var (channel, handler) = CreateChannel(CreateGoogleChatOptions());
        var result = await channel.SendAsync(AlertType.ZombieOccurrenceDetected, CreatePayload());

        result.Success.Should().BeTrue();
        result.ChannelName.Should().Be("GoogleChat");

        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_ShouldReturnFailed_WhenWebhookReturnsError()
    {
        var (channel, _) = CreateChannel(CreateGoogleChatOptions(), HttpStatusCode.Forbidden, "forbidden1");
        var result = await channel.SendAsync(AlertType.DatabaseConnectionFailed, CreatePayload());

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("forbidden1");
    }

    [Fact]
    public async Task SendAsync_ShouldReturnSkipped_WhenNoWebhookConfigured()
    {
        var options = new AlertingOptions
        {
            SendOnlyInProduction = false,
            Channels = new AlertChannelsOptions
            {
                GoogleChat = new GoogleChatChannelOptions
                {
                    Enabled = true,
                    SendOnlyInProduction = false,
                    DefaultSpace = "nonexistent",
                    Spaces = []
                }
            }
        };
        var (channel, _) = CreateChannel(options);
        var result = await channel.SendAsync(AlertType.JobAutoDisabled, CreatePayload());

        result.Message.Should().Contain("No webhook");
    }

    [Fact]
    public async Task SendAsync_ShouldAppendThreadReplyOption_WhenThreadKeyIsSet()
    {
        Uri capturedUri = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedUri = req.RequestUri)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });

        var httpClient = new HttpClient(handlerMock.Object);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient(nameof(GoogleChatAlertChannel))).Returns(httpClient);

        var loggerMock = new Mock<IMilvaLogger>();
        var channel = new GoogleChatAlertChannel(
            Options.Create(CreateGoogleChatOptions()),
            httpClientFactoryMock.Object,
            new Lazy<IMilvaLogger>(() => loggerMock.Object));

        var payload = CreatePayload();
        payload.ThreadKey = "job-failed-123";

        // Act
        await channel.SendAsync(AlertType.JobExecutionFailed, payload);

        // Assert
        capturedUri.Should().NotBeNull();
        capturedUri!.ToString().Should().Contain("messageReplyOption=REPLY_MESSAGE_FALLBACK_TO_NEW_THREAD");
    }

    [Fact]
    public async Task SendAsync_ShouldNotAppendThreadReplyOption_WhenThreadKeyIsNull()
    {
        Uri capturedUri = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>((req, _) => capturedUri = req.RequestUri)
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") });

        var httpClient = new HttpClient(handlerMock.Object);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient(nameof(GoogleChatAlertChannel))).Returns(httpClient);

        var loggerMock = new Mock<IMilvaLogger>();
        var channel = new GoogleChatAlertChannel(
            Options.Create(CreateGoogleChatOptions()),
            httpClientFactoryMock.Object,
            new Lazy<IMilvaLogger>(() => loggerMock.Object));

        var payload = CreatePayload();
        payload.ThreadKey = null;

        // Act
        await channel.SendAsync(AlertType.JobExecutionFailed, payload);

        // Assert
        capturedUri.Should().NotBeNull();
        capturedUri!.ToString().Should().NotContain("messageReplyOption");
    }
}
