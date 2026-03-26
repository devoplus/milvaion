namespace ReporterWorker.Models;

public static class MetricTypes
{
    public const string FailureRateTrend = "FailureRateTrend";
    public const string PercentileDurations = "PercentileDurations";
    public const string TopSlowJobs = "TopSlowJobs";
    public const string WorkerThroughput = "WorkerThroughput";
    public const string WorkerUtilizationTrend = "WorkerUtilizationTrend";
    public const string CronScheduleVsActual = "CronScheduleVsActual";
    public const string JobHealthScore = "JobHealthScore";
    public const string WorkflowSuccessRate = "WorkflowSuccessRate";
    public const string WorkflowStepBottleneck = "WorkflowStepBottleneck";
    public const string WorkflowDurationTrend = "WorkflowDurationTrend";
}
