using Milvaion.Application.Dtos.AlertingDtos;

namespace Milvaion.Application.Interfaces;

/// <summary>
/// Provides a unified interface for sending alerts through multiple channels.
/// </summary>
public interface IAlertNotifier
{
    /// <summary>
    /// Sends an alert to all configured channels for the specified alert type.
    /// This method awaits the completion of all channel operations.
    /// </summary>
    /// <param name="alertType">The type of alert to send.</param>
    /// <param name="payload">The alert payload containing the notification data.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The aggregated result from all channels.</returns>
    Task<AlertResult> SendAsync(AlertType alertType, AlertPayload payload, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends an alert in a fire-and-forget manner. This method returns immediately
    /// and does not block the calling thread. All errors are handled internally.
    /// Use this method when you don't need to know the result of the alert operation.
    /// </summary>
    /// <param name="alertType">The type of alert to send.</param>
    /// <param name="payload">The alert payload containing the notification data.</param>
    void SendFireAndForget(AlertType alertType, AlertPayload payload);

    /// <summary>
    /// Checks if the specified alert type is enabled.
    /// </summary>
    /// <param name="alertType">The alert type to check.</param>
    /// <returns>True if the alert type is enabled; otherwise, false.</returns>
    bool IsAlertEnabled(AlertType alertType);

    /// <summary>
    /// Gets the configured routes (channel names) for the specified alert type.
    /// </summary>
    /// <param name="alertType">The alert type to get routes for.</param>
    /// <returns>A list of channel names that the alert will be routed to.</returns>
    IReadOnlyList<string> GetRoutesForAlert(AlertType alertType);
}
