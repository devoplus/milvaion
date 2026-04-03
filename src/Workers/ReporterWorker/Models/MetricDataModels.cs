namespace ReporterWorker.Models;

public class FailureRateTrendData
{
    public List<TimeSeriesPoint> DataPoints { get; set; } = [];
    public double ThresholdPercentage { get; set; }
}

public class PercentileDurationsData
{
    public Dictionary<string, PercentileData> Jobs { get; set; } = [];
}

public class PercentileData
{
    public double P50 { get; set; }
    public double P95 { get; set; }
    public double P99 { get; set; }
}

public class TopSlowJobsData
{
    public List<JobDurationInfo> Jobs { get; set; } = [];
}

public class JobDurationInfo
{
    public string JobName { get; set; }
    public double AverageDurationMs { get; set; }
    public int OccurrenceCount { get; set; }
}

public class WorkerThroughputData
{
    public List<WorkerThroughputInfo> Workers { get; set; } = [];
}

public class WorkerThroughputInfo
{
    public string WorkerId { get; set; }
    public int JobCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double AverageDurationMs { get; set; }
}

public class WorkerUtilizationTrendData
{
    public List<UtilizationPoint> DataPoints { get; set; } = [];
}

public class UtilizationPoint
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, double> WorkerUtilization { get; set; } = [];
}

public class CronScheduleVsActualData
{
    public List<ScheduleDeviationInfo> Jobs { get; set; } = [];
}

public class ScheduleDeviationInfo
{
    public Guid OccurrenceId { get; set; }
    public Guid JobId { get; set; }
    public string JobName { get; set; }
    public DateTime ScheduledTime { get; set; }
    public DateTime ActualTime { get; set; }
    public double DeviationSeconds { get; set; }
}

public class JobHealthScoreData
{
    public List<JobHealthInfo> Jobs { get; set; } = [];
}

public class JobHealthInfo
{
    public string JobName { get; set; }
    public double SuccessRate { get; set; }
    public int TotalOccurrences { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
}

public class WorkflowSuccessRateData
{
    public List<WorkflowHealthInfo> Workflows { get; set; } = [];
}

public class WorkflowHealthInfo
{
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; }
    public double SuccessRate { get; set; }
    public int TotalRuns { get; set; }
    public int CompletedCount { get; set; }
    public int FailedCount { get; set; }
    public int PartialCount { get; set; }
    public int CancelledCount { get; set; }
    public double AvgDurationMs { get; set; }
}

public class WorkflowStepBottleneckData
{
    public List<WorkflowBottleneckInfo> Workflows { get; set; } = [];
}

public class WorkflowBottleneckInfo
{
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; }
    public List<StepPerformanceInfo> Steps { get; set; } = [];
}

public class StepPerformanceInfo
{
    public string StepName { get; set; }
    public double AvgDurationMs { get; set; }
    public double MaxDurationMs { get; set; }
    public int ExecutionCount { get; set; }
    public int FailureCount { get; set; }
    public int SkippedCount { get; set; }
    public int RetryCount { get; set; }
}

public class WorkflowDurationTrendData
{
    public List<WorkflowDurationPoint> DataPoints { get; set; } = [];
}

public class WorkflowDurationPoint
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, double> WorkflowAvgDurationMs { get; set; } = [];
}

public class TimeSeriesPoint
{
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
}
