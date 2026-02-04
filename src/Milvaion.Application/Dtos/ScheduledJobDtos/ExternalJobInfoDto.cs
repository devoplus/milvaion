using Milvasoft.Attributes.Annotations;

namespace Milvaion.Application.Dtos.ScheduledJobDtos;

/// <summary>
/// Data transfer object for scheduledjob list.
/// </summary>
[Translate]
public class ExternalJobInfoDto
{
    /// <summary>
    /// Indicates whether this job is from an external scheduler (Quartz, Hangfire, etc.).
    /// External jobs are not dispatched by Milvaion - they only report their occurrences for monitoring.
    /// </summary>
    public bool IsExternal { get; set; }

    /// <summary>
    /// External job identifier for mapping (e.g., "DEFAULT.MyQuartzJob").
    /// Used to correlate occurrences from external schedulers.
    /// </summary>
    public string ExternalJobId { get; set; }
}
