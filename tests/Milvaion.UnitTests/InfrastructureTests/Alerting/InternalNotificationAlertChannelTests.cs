using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Milvaion.Application.Dtos.AlertingDtos;
using Milvaion.Application.Dtos.NotificationDtos;
using Milvaion.Application.Interfaces;
using Milvaion.Application.Utils.Models.Options;
using Milvaion.Domain.Enums;
using Milvaion.Infrastructure.Services.Alerting;
using Milvaion.Infrastructure.Services.Alerting.Channels;
using Milvasoft.Core.Abstractions;
using Moq;

namespace Milvaion.UnitTests.InfrastructureTests.Alerting;

[Trait("Infrastructure Unit Tests", "InternalNotificationAlertChannel unit tests.")]
public class InternalNotificationAlertChannelTests
{
    private static AlertPayload CreatePayload() => new()
    {
        Title = "Internal Alert",
        Message = "Job auto-disabled",
        Severity = AlertSeverity.Warning,
        Source = "StatusTracker",
        Timestamp = DateTime.UtcNow,
        ActionLink = "/jobs/789"
    };

    private static AlertingOptions CreateOptions(bool enabled = true, bool sendOnlyInProduction = false)
    => new()
    {
        SendOnlyInProduction = sendOnlyInProduction,
        Channels = new AlertChannelsOptions
        {
            InternalNotification = new InternalNotificationChannelOptions
            {
                Enabled = enabled,
                SendOnlyInProduction = sendOnlyInProduction
            }
        }
    };

    private static (InternalNotificationAlertChannel Channel, Mock<INotificationService> NotificationMock) CreateChannel(AlertingOptions options)
    {
        var notificationMock = new Mock<INotificationService>();
        notificationMock.Setup(n => n.PublishAsync(It.IsAny<InternalNotificationRequest>(), It.IsAny<System.Linq.Expressions.Expression<Func<Milvaion.Domain.User, bool>>>(), It.IsAny<CancellationToken>()))
                        .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddScoped(_ => notificationMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var loggerMock = new Mock<IMilvaLogger>();
        var channel = new InternalNotificationAlertChannel(
            Options.Create(options),
            serviceProvider,
            new Lazy<IMilvaLogger>(() => loggerMock.Object));

        return (channel, notificationMock);
    }

    [Fact]
    public void ChannelName_ShouldBeInternalNotification()
    {
        var (channel, _) = CreateChannel(CreateOptions());
        channel.ChannelName.Should().Be("InternalNotification");
    }

    [Fact]
    public void IsEnabled_ShouldReturnTrue_WhenEnabled()
    {
        var (channel, _) = CreateChannel(CreateOptions(enabled: true));
        channel.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public void IsEnabled_ShouldReturnFalse_WhenDisabled()
    {
        var (channel, _) = CreateChannel(CreateOptions(enabled: false));
        channel.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SendAsync_ShouldReturnSkipped_WhenDisabled()
    {
        var (channel, notificationMock) = CreateChannel(CreateOptions(enabled: false));

        var result = await channel.SendAsync(AlertType.JobAutoDisabled, CreatePayload());

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("disabled");
        notificationMock.Verify(n => n.PublishAsync(It.IsAny<InternalNotificationRequest>(), It.IsAny<System.Linq.Expressions.Expression<Func<Milvaion.Domain.User, bool>>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_ShouldCallNotificationService_WhenEnabled()
    {
        var (channel, notificationMock) = CreateChannel(CreateOptions());

        var result = await channel.SendAsync(AlertType.JobAutoDisabled, CreatePayload());

        result.Success.Should().BeTrue();
        result.ChannelName.Should().Be("InternalNotification");
        notificationMock.Verify(n => n.PublishAsync(
            It.Is<InternalNotificationRequest>(r =>
                r.Type == AlertType.JobAutoDisabled &&
                r.Text.Contains("Internal Alert") &&
                r.ActionLink == "/jobs/789"),
            It.IsAny<System.Linq.Expressions.Expression<Func<Milvaion.Domain.User, bool>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldIncludeMessage_WhenPayloadHasMessage()
    {
        var (channel, notificationMock) = CreateChannel(CreateOptions());

        var payload = CreatePayload();
        payload.Message = "Job failed 5 times";

        await channel.SendAsync(AlertType.JobAutoDisabled, payload);

        notificationMock.Verify(n => n.PublishAsync(
            It.Is<InternalNotificationRequest>(r => r.Text.Contains("Job failed 5 times")),
            It.IsAny<System.Linq.Expressions.Expression<Func<Milvaion.Domain.User, bool>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldSetFindRecipientsFromType_WhenNoExplicitRecipients()
    {
        var (channel, notificationMock) = CreateChannel(CreateOptions());

        var payload = CreatePayload();
        payload.Recipients = null;

        await channel.SendAsync(AlertType.WorkerDisconnected, payload);

        notificationMock.Verify(n => n.PublishAsync(
            It.Is<InternalNotificationRequest>(r => r.FindRecipientsFromType == true),
            It.IsAny<System.Linq.Expressions.Expression<Func<Milvaion.Domain.User, bool>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldSetExplicitRecipients_WhenProvided()
    {
        var (channel, notificationMock) = CreateChannel(CreateOptions());

        var payload = CreatePayload();
        payload.Recipients = ["admin", "devops"];

        await channel.SendAsync(AlertType.QueueDepthCritical, payload);

        notificationMock.Verify(n => n.PublishAsync(
            It.Is<InternalNotificationRequest>(r =>
                r.FindRecipientsFromType == false &&
                r.Recipients.Contains("admin") &&
                r.Recipients.Contains("devops")),
            It.IsAny<System.Linq.Expressions.Expression<Func<Milvaion.Domain.User, bool>>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_ShouldHandleException_AndReturnFailed()
    {
        var notificationMock = new Mock<INotificationService>();
        notificationMock.Setup(n => n.PublishAsync(It.IsAny<InternalNotificationRequest>(), It.IsAny<System.Linq.Expressions.Expression<Func<Milvaion.Domain.User, bool>>>(), It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new InvalidOperationException("DB connection failed"));

        var services = new ServiceCollection();
        services.AddScoped(_ => notificationMock.Object);
        var serviceProvider = services.BuildServiceProvider();

        var loggerMock = new Mock<IMilvaLogger>();
        var channel = new InternalNotificationAlertChannel(
            Options.Create(CreateOptions()),
            serviceProvider,
            new Lazy<IMilvaLogger>(() => loggerMock.Object));

        var result = await channel.SendAsync(AlertType.DatabaseConnectionFailed, CreatePayload());

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("DB connection failed");
    }
}
