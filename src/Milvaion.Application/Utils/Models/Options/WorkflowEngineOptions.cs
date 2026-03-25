namespace Milvaion.Application.Utils.Models.Options;

/// <summary>
/// Configuration options for the Workflow Engine background service.
/// </summary>
public class WorkflowEngineOptions : BackgroundServiceOptions
{
    /// <summary>
    /// Configuration section key.
    /// </summary>
    public const string SectionKey = "MilvaionConfig:WorkflowEngine";

    /// <summary>
    /// Polling interval in seconds (how often to check for active workflow runs).
    /// Default: 5 seconds.
    /// </summary>
    public int PollingIntervalSeconds { get; set; } = 5;
}
