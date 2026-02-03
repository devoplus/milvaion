namespace Milvasoft.Milvaion.Sdk.Worker.Quartz.Options;

/// <summary>
/// Configuration options for Milvaion Quartz integration.
/// </summary>
public class MilvaionExternalSchedulerOptions
{
    /// <summary>
    /// Configuration section key.
    /// </summary>
    public const string SectionKey = "Worker:ExternalScheduler";

    /// <summary>
    /// Source identifier for external jobs. (e.g., "Quartz")
    /// </summary>
    public string Source { get; set; }
}