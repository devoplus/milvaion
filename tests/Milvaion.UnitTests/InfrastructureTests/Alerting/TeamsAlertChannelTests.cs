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

[Trait("Infrastructure Unit Tests", "TeamsAlertChannel unit tests.")]
public class TeamsAlertChannelTests
{
    private static AlertPayload CreatePayload(AlertSeverity severity = AlertSeverity.Error) => new()
    {
        Title = "Test Teams Alert",
        Message = "Something critical happened",
        Severity = severity,
        Source = "UnitTest",
        Timestamp = DateTime.UtcNow,
        AdditionalData = new { JobId = "abc-123" }
    };

    private static AlertingOptions CreateTeamsOptions(bool enabled = true, bool sendOnlyInProduction = false)
    => new()
    {
        SendOnlyInProduction = sendOnlyInProduction,
        Channels = new AlertChannelsOptions
        {
            Teams = new TeamsChannelOptions
            {
                Enabled = enabled,
                SendOnlyInProduction = sendOnlyInProduction,
                DefaultChannel = "alerts",
                Channels =
                [
                    new TeamsChannelConfig
                    {
                        Channel = "alerts",
                        WebhookUrl = "https://outlook.office.com/webhook/fake-webhook-url"
                    }
                ]
            }
        }
    };

    private static (TeamsAlertChannel Channel, Mock<HttpMessageHandler> Handler) CreateChannel(AlertingOptions options, HttpStatusCode responseStatus = HttpStatusCode.OK, string responseBody = "{}")
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
        httpClientFactoryMock.Setup(f => f.CreateClient(nameof(TeamsAlertChannel))).Returns(httpClient);

        var loggerMock = new Mock<IMilvaLogger>();
        var channel = new TeamsAlertChannel(
            Options.Create(options),
            httpClientFactoryMock.Object,
            new Lazy<IMilvaLogger>(() => loggerMock.Object));

        return (channel, handlerMock);
    }

    [Fact]
    public void ChannelName_ShouldBeTeams()
    {
        var (channel, _) = CreateChannel(CreateTeamsOptions());
        channel.ChannelName.Should().Be("Teams");
    }

    [Fact]
    public void IsEnabled_ShouldReturnTrue_WhenEnabled()
    {
        var (channel, _) = CreateChannel(CreateTeamsOptions(enabled: true));
        channel.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_ShouldReturnFalse_WhenDisabled()
    {
        var (channel, _) = CreateChannel(CreateTeamsOptions(enabled: false));
        channel.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_ShouldReturnSkipped_WhenDisabled()
    {
        var (channel, _) = CreateChannel(CreateTeamsOptions(enabled: false));
        var result = await channel.SendAsync(AlertType.JobAutoDisabled, CreatePayload());
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("disabled");
    }

    [Fact]
    public async Task SendAsync_ShouldReturnSuccess_WhenWebhookReturnsOk()
    {
        var (channel, handler) = CreateChannel(CreateTeamsOptions());
        var result = await channel.SendAsync(AlertType.ZombieOccurrenceDetected, CreatePayload());

        result.Success.Should().BeTrue();
        result.ChannelName.Should().Be("Teams");

        handler.Protected().Verify("SendAsync", Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r => r.Method == HttpMethod.Post),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_ShouldReturnFailed_WhenWebhookReturnsError()
    {
        var (channel, _) = CreateChannel(CreateTeamsOptions(), HttpStatusCode.Forbidden, "forbidden1");
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
                Teams = new TeamsChannelOptions
                {
                    Enabled = true,
                    SendOnlyInProduction = false,
                    DefaultChannel = "nonexistent",
                    Channels = []
                }
            }
        };
        var (channel, _) = CreateChannel(options);
        var result = await channel.SendAsync(AlertType.JobAutoDisabled, CreatePayload());

        result.Message.Should().Contain("No webhook");
    }

    [Fact]
    public async Task SendAsync_ShouldSendAdaptiveCardPayload()
    {
        string capturedBody = null;
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                capturedBody = await req.Content.ReadAsStringAsync();
            })
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("{}")
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        httpClientFactoryMock.Setup(f => f.CreateClient(nameof(TeamsAlertChannel))).Returns(httpClient);

        var loggerMock = new Mock<IMilvaLogger>();
        var channel = new TeamsAlertChannel(
            Options.Create(CreateTeamsOptions()),
            httpClientFactoryMock.Object,
            new Lazy<IMilvaLogger>(() => loggerMock.Object));

        await channel.SendAsync(AlertType.JobAutoDisabled, CreatePayload());

        capturedBody.Should().NotBeNullOrEmpty();
        capturedBody.Should().Contain("application/vnd.microsoft.card.adaptive");
        capturedBody.Should().Contain("AdaptiveCard");
    }

    [Theory]
    [InlineData(AlertSeverity.Info)]
    [InlineData(AlertSeverity.Warning)]
    [InlineData(AlertSeverity.Error)]
    [InlineData(AlertSeverity.Critical)]
    public async Task SendAsync_ShouldHandleAllSeverityLevels(AlertSeverity severity)
    {
        var (channel, _) = CreateChannel(CreateTeamsOptions());
        var result = await channel.SendAsync(AlertType.JobAutoDisabled, CreatePayload(severity));
        result.Success.Should().BeTrue();
    }
}
