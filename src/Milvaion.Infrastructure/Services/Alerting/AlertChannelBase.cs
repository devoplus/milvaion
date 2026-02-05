using Microsoft.Extensions.Options;
using Milvaion.Application.Dtos.AlertingDtos;
using Milvaion.Application.Interfaces;
using Milvaion.Application.Utils.Extensions;

namespace Milvaion.Infrastructure.Services.Alerting;

/// <summary>
/// Base class for alert channels providing common functionality.
/// </summary>
/// <typeparam name="TChannelOptions">The type of channel-specific options.</typeparam>
public abstract class AlertChannelBase<TChannelOptions> : IAlertChannel where TChannelOptions : ChannelConfigBase
{
    /// <summary>
    /// The alerting options.
    /// </summary>
    protected readonly AlertingOptions _alertingOptions;

    /// <summary>
    /// The channel-specific options.
    /// </summary>
    protected readonly TChannelOptions _channelOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlertChannelBase{TChannelOptions}"/> class.
    /// </summary>
    /// <param name="alertingOptions">The alerting options.</param>
    protected AlertChannelBase(IOptions<AlertingOptions> alertingOptions)
    {
        _alertingOptions = alertingOptions.Value;
        _channelOptions = GetChannelOptions(_alertingOptions);
    }

    /// <inheritdoc/>
    public abstract string ChannelName { get; }

    /// <inheritdoc/>
    public virtual bool IsEnabled => _channelOptions?.Enabled ?? false;

    /// <summary>
    /// Gets the channel-specific options from the alerting options.
    /// </summary>
    /// <param name="options">The alerting options.</param>
    /// <returns>The channel-specific options.</returns>
    protected abstract TChannelOptions GetChannelOptions(AlertingOptions options);

    /// <inheritdoc/>
    public virtual bool CanSend()
    {
        if (!IsEnabled)
            return false;

        var sendOnlyInProduction = _channelOptions.SendOnlyInProduction ?? _alertingOptions.SendOnlyInProduction;

        if (sendOnlyInProduction && !MilvaionExtensions.IsCurrentEnvProduction())
            return false;

        return true;
    }

    /// <inheritdoc/>
    public async Task<ChannelResult> SendAsync(AlertType alertType, AlertPayload payload, CancellationToken cancellationToken = default)
    {
        if (!CanSend())
            return ChannelResult.Skipped(ChannelName, "Channel is disabled or environment check failed");

        try
        {
            return await SendCoreAsync(alertType, payload, cancellationToken);
        }
        catch (Exception ex)
        {
            return ChannelResult.Failed(ChannelName, ex.Message, ex);
        }
    }

    /// <summary>
    /// Performs the actual send operation. Override this in derived classes.
    /// </summary>
    /// <param name="alertType">The type of alert being sent.</param>
    /// <param name="payload">The alert payload.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the send operation.</returns>
    protected abstract Task<ChannelResult> SendCoreAsync(AlertType alertType, AlertPayload payload, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the severity color for visual indicators.
    /// </summary>
    /// <param name="severity">The alert severity.</param>
    /// <returns>A color string appropriate for the channel.</returns>
    protected static string GetSeverityColor(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Info => "#2196F3",      // Blue
        AlertSeverity.Warning => "#FF9800",   // Orange
        AlertSeverity.Error => "#F44336",     // Red
        AlertSeverity.Critical => "#9C27B0",  // Purple
        _ => "#757575"                         // Grey
    };

    /// <summary>
    /// Gets the severity emoji for text-based channels.
    /// </summary>
    /// <param name="severity">The alert severity.</param>
    /// <returns>An emoji representing the severity.</returns>
    protected static string GetSeverityEmoji(AlertSeverity severity) => severity switch
    {
        AlertSeverity.Info => "ℹ️",
        AlertSeverity.Warning => "⚠️",
        AlertSeverity.Error => "❌",
        AlertSeverity.Critical => "🚨",
        _ => "📢"
    };
}
