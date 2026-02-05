using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Milvaion.Application.Dtos.AlertingDtos;
using Milvaion.Application.Dtos.NotificationDtos;
using Milvaion.Application.Interfaces;
using Milvasoft.Core.Abstractions;

namespace Milvaion.Infrastructure.Services.Alerting.Channels;

/// <summary>
/// Alert channel implementation for Internal Notifications (database-stored notifications for users).
/// Uses IServiceProvider to create scoped INotificationService instances since this channel is registered as Singleton.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="InternalNotificationAlertChannel"/> class.
/// </remarks>
public class InternalNotificationAlertChannel(IOptions<AlertingOptions> alertingOptions,
                                              IServiceProvider serviceProvider,
                                              Lazy<IMilvaLogger> logger) : AlertChannelBase<InternalNotificationChannelOptions>(alertingOptions)
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly Lazy<IMilvaLogger> _logger = logger;

    /// <inheritdoc/>
    public override string ChannelName => nameof(AlertChannelType.InternalNotification);

    /// <inheritdoc/>
    protected override InternalNotificationChannelOptions GetChannelOptions(AlertingOptions options)
        => options.Channels?.InternalNotification;

    /// <inheritdoc/>
    protected override async Task<ChannelResult> SendCoreAsync(AlertType alertType, AlertPayload payload, CancellationToken cancellationToken)
    {
        // Create a scope to resolve scoped INotificationService
        await using var scope = _serviceProvider.CreateAsyncScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var request = new InternalNotificationRequest
        {
            Type = alertType,
            Text = FormatNotificationText(alertType, payload),
            ActionLink = payload.ActionLink,
            Data = new
            {
                AlertType = alertType.ToString(),
                payload.Severity,
                payload.Source,
                payload.Timestamp,
                payload.AdditionalData
            },
            Recipients = payload.Recipients,
            FindRecipientsFromType = payload.Recipients == null || payload.Recipients.Count == 0
        };

        await notificationService.PublishAsync(request, payload.UserFilter, cancellationToken);

        return ChannelResult.Successful(ChannelName);
    }

    private static string FormatNotificationText(AlertType alertType, AlertPayload payload)
    {
        var severityEmoji = GetSeverityEmoji(payload.Severity);
        var title = payload.Title ?? alertType.ToString();

        if (!string.IsNullOrWhiteSpace(payload.Message))
            return $"{severityEmoji} {title}: {payload.Message}";

        return $"{severityEmoji} {title}";
    }
}
