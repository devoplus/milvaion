using Milvaion.Application.Dtos.AlertingDtos;

namespace Milvaion.Application.Interfaces;

/// <summary>
/// Defines the contract for alert notification channels.
/// </summary>
public interface IAlertChannel
{
    /// <summary>
    /// Gets the unique name of this channel.
    /// </summary>
    string ChannelName { get; }

    /// <summary>
    /// Gets whether this channel is enabled based on configuration.
    /// </summary>
    bool IsEnabled { get; }

    /// <summary>
    /// Determines whether this channel can send alerts in the current environment.
    /// </summary>
    /// <returns>True if the channel can send alerts; otherwise, false.</returns>
    bool CanSend();

    /// <summary>
    /// Sends an alert through this channel.
    /// </summary>
    /// <param name="alertType">The type of alert being sent.</param>
    /// <param name="payload">The alert payload containing the notification data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result of the send operation.</returns>
    Task<ChannelResult> SendAsync(AlertType alertType, AlertPayload payload, CancellationToken cancellationToken = default);
}
