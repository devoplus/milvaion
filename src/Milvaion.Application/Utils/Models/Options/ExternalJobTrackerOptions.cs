namespace Milvaion.Application.Utils.Models.Options;

/// <summary>
/// Configuration options for the external job consumer service.
/// </summary>
public class ExternalJobTrackerOptions : BackgroundServiceOptions
{
    /// <summary>
    /// Configuration section key.
    /// </summary>
    public const string SectionKey = "MilvaionConfig:ExternalJobTracker";

    /// <summary>
    /// Batch size for processing job registration messages.
    /// </summary>
    public int RegistrationBatchSize { get; set; } = 50;

    /// <summary>
    /// Batch size for processing occurrence messages.
    /// </summary>
    public int OccurrenceBatchSize { get; set; } = 100;

    /// <summary>
    /// Interval in milliseconds between batch processing cycles.
    /// </summary>
    public int BatchIntervalMs { get; set; } = 1000;
}
