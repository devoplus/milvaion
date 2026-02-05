using System.Linq.Expressions;

namespace Milvaion.Application.Dtos.AlertingDtos;

/// <summary>
/// Represents the payload data for an alert notification.
/// </summary>
public class AlertPayload
{
    /// <summary>
    /// Gets or sets the title of the alert.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the main message content of the alert.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the severity level of the alert.
    /// </summary>
    public AlertSeverity Severity { get; set; } = AlertSeverity.Info;

    /// <summary>
    /// Gets or sets additional structured data associated with the alert.
    /// </summary>
    public object AdditionalData { get; set; }

    /// <summary>
    /// Gets or sets the source or origin of the alert.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the alert was generated.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets an optional action link for the alert.
    /// </summary>
    public string ActionLink { get; set; }

    /// <summary>
    /// Gets or sets optional user filter expression for internal notifications.
    /// When set, internal notifications will only be sent to users matching this expression.
    /// </summary>
    public Expression<Func<User, bool>> UserFilter { get; set; }

    /// <summary>
    /// Gets or sets specific recipient usernames for internal notifications.
    /// </summary>
    public List<string> Recipients { get; set; }

    /// <summary>
    /// Gets or sets the thread key for grouping messages in Google Chat.
    /// When set, messages with the same thread key will be grouped together.
    /// </summary>
    public string ThreadKey { get; set; }
}
