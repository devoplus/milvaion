using Milvasoft.Attributes.Annotations;
using Milvasoft.Core.EntityBases.Concrete.Auditing;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Milvasoft.Milvaion.Sdk.Domain;

/// <summary>
/// Entity representing a generated metric report with statistical data.
/// </summary>
[Table(SchedulerTableNames.MetricReports)]
[DontIndexCreationDate]
public class MetricReport : CreationAuditableEntity<Guid>
{
    /// <summary>
    /// Type of the metric (e.g., FailureRate, P50P95P99, TopSlowJobs, WorkerThroughput, etc.)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string MetricType { get; set; }

    /// <summary>
    /// Display name of the metric for UI
    /// </summary>
    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; }

    /// <summary>
    /// Description of the metric
    /// </summary>
    [MaxLength(500)]
    public string Description { get; set; }

    /// <summary>
    /// JSON serialized metric data
    /// </summary>
    [Required]
    [Column(TypeName = "jsonb")]
    public string Data { get; set; }

    /// <summary>
    /// Start time of the data period (UTC)
    /// </summary>
    [Required]
    public DateTime PeriodStartTime { get; set; }

    /// <summary>
    /// End time of the data period (UTC)
    /// </summary>
    [Required]
    public DateTime PeriodEndTime { get; set; }

    /// <summary>
    /// Timestamp when the report was generated (UTC)
    /// </summary>
    [Required]
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Tags for categorization and filtering (comma separated)
    /// </summary>
    [MaxLength(500)]
    public string Tags { get; set; }
}
