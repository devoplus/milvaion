namespace Milvaion.Application.Dtos.AlertingDtos;

/// <summary>
/// Defines the severity levels for alerts.
/// </summary>
public enum AlertSeverity
{
    /// <summary>
    /// Informational alert - no action required.
    /// </summary>
    Info = 0,

    /// <summary>
    /// Warning alert - attention may be needed.
    /// </summary>
    Warning = 1,

    /// <summary>
    /// Error alert - action should be taken.
    /// </summary>
    Error = 2,

    /// <summary>
    /// Critical alert - immediate action required.
    /// </summary>
    Critical = 3
}
