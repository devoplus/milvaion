using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Milvaion.Infrastructure.Telemetry;

/// <summary>
/// Custom OpenTelemetry metrics for Milvaion background services.
/// </summary>
public sealed class BackgroundServiceMetrics : IDisposable
{
    /// <summary>
    /// The meter name used for OpenTelemetry registration.
    /// </summary>
    public const string MeterName = "Milvaion.BackgroundServices";

    private readonly Meter _meter;

    // Job Dispatcher Metrics
    private readonly Counter<long> _jobsDispatched;
    private readonly Counter<long> _jobDispatchFailures;
    private readonly Histogram<double> _dispatchDuration;
    private readonly ObservableGauge<int> _pendingJobsGauge;
    private int _pendingJobsCount;

    // Status Tracker Metrics
    private readonly Counter<long> _statusUpdatesProcessed;
    private readonly Counter<long> _statusUpdateFailures;
    private readonly Histogram<double> _statusUpdateDuration;
    private readonly Counter<long> _statusUpdatesByStatus;
    private readonly ObservableGauge<int> _statusBatchSizeGauge;
    private int _statusBatchSize;

    // Log Collector Metrics
    private readonly Counter<long> _logsCollected;
    private readonly Counter<long> _logCollectionFailures;
    private readonly Histogram<double> _logBatchDuration;
    private readonly ObservableGauge<int> _logBatchSizeGauge;
    private int _logBatchSize;

    // Worker Discovery Metrics
    private readonly Counter<long> _workerRegistrations;
    private readonly Counter<long> _workerHeartbeats;
    private readonly Counter<long> _workerHeartbeatFailures;
    private readonly Histogram<double> _heartbeatProcessDuration;
    private readonly ObservableGauge<int> _activeWorkersGauge;
    private int _activeWorkersCount;

    // Zombie Detector Metrics
    private readonly Counter<long> _zombieOccurrencesDetected;
    private readonly Counter<long> _zombieOccurrencesRecovered;
    private readonly Histogram<double> _zombieDetectionDuration;

    // Failed Occurrence Handler Metrics
    private readonly Counter<long> _failedOccurrencesProcessed;
    private readonly Counter<long> _failedOccurrencesRetried;
    private readonly Histogram<double> _failedOccurrenceProcessDuration;

    // General Background Service Metrics
    private readonly Counter<long> _serviceIterations;
    private readonly Counter<long> _serviceErrors;
    private readonly Histogram<double> _iterationDuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="BackgroundServiceMetrics"/> class.
    /// </summary>
    public BackgroundServiceMetrics(IMeterFactory meterFactory = null)
    {
        _meter = meterFactory?.Create(MeterName) ?? new Meter(MeterName, "1.0.0");

        // Job Dispatcher
        _jobsDispatched = _meter.CreateCounter<long>(
            "milvaion.dispatcher.jobs_dispatched",
            unit: "{job}",
            description: "Total number of jobs dispatched to workers");

        _jobDispatchFailures = _meter.CreateCounter<long>(
            "milvaion.dispatcher.dispatch_failures",
            unit: "{failure}",
            description: "Total number of job dispatch failures");

        _dispatchDuration = _meter.CreateHistogram<double>(
            "milvaion.dispatcher.dispatch_duration",
            unit: "ms",
            description: "Duration of job dispatch operations");

        _pendingJobsGauge = _meter.CreateObservableGauge(
            "milvaion.dispatcher.pending_jobs",
            () => _pendingJobsCount,
            unit: "{job}",
            description: "Number of jobs pending dispatch");

        // Status Tracker
        _statusUpdatesProcessed = _meter.CreateCounter<long>(
            "milvaion.status_tracker.updates_processed",
            unit: "{update}",
            description: "Total number of status updates processed");

        _statusUpdateFailures = _meter.CreateCounter<long>(
            "milvaion.status_tracker.update_failures",
            unit: "{failure}",
            description: "Total number of status update failures");

        _statusUpdateDuration = _meter.CreateHistogram<double>(
            "milvaion.status_tracker.batch_duration",
            unit: "ms",
            description: "Duration of status update batch processing");

        _statusUpdatesByStatus = _meter.CreateCounter<long>(
            "milvaion.status_tracker.updates_by_status",
            unit: "{update}",
            description: "Status updates by final status");

        _statusBatchSizeGauge = _meter.CreateObservableGauge(
            "milvaion.status_tracker.batch_size",
            () => _statusBatchSize,
            unit: "{update}",
            description: "Current status update batch size");

        // Log Collector
        _logsCollected = _meter.CreateCounter<long>(
            "milvaion.log_collector.logs_collected",
            unit: "{log}",
            description: "Total number of worker logs collected");

        _logCollectionFailures = _meter.CreateCounter<long>(
            "milvaion.log_collector.collection_failures",
            unit: "{failure}",
            description: "Total number of log collection failures");

        _logBatchDuration = _meter.CreateHistogram<double>(
            "milvaion.log_collector.batch_duration",
            unit: "ms",
            description: "Duration of log batch processing");

        _logBatchSizeGauge = _meter.CreateObservableGauge(
            "milvaion.log_collector.batch_size",
            () => _logBatchSize,
            unit: "{log}",
            description: "Current log batch size");

        // Worker Discovery
        _workerRegistrations = _meter.CreateCounter<long>(
            "milvaion.worker_discovery.registrations",
            unit: "{registration}",
            description: "Total number of worker registrations");

        _workerHeartbeats = _meter.CreateCounter<long>(
            "milvaion.worker_discovery.heartbeats",
            unit: "{heartbeat}",
            description: "Total number of worker heartbeats received");

        _workerHeartbeatFailures = _meter.CreateCounter<long>(
            "milvaion.worker_discovery.heartbeat_failures",
            unit: "{failure}",
            description: "Total number of heartbeat processing failures");

        _heartbeatProcessDuration = _meter.CreateHistogram<double>(
            "milvaion.worker_discovery.heartbeat_duration",
            unit: "ms",
            description: "Duration of heartbeat batch processing");

        _activeWorkersGauge = _meter.CreateObservableGauge(
            "milvaion.worker_discovery.active_workers",
            () => _activeWorkersCount,
            unit: "{worker}",
            description: "Number of currently active workers");

        // Zombie Detector
        _zombieOccurrencesDetected = _meter.CreateCounter<long>(
            "milvaion.zombie_detector.detected",
            unit: "{occurrence}",
            description: "Total number of zombie occurrences detected");

        _zombieOccurrencesRecovered = _meter.CreateCounter<long>(
            "milvaion.zombie_detector.recovered",
            unit: "{occurrence}",
            description: "Total number of zombie occurrences recovered");

        _zombieDetectionDuration = _meter.CreateHistogram<double>(
            "milvaion.zombie_detector.detection_duration",
            unit: "ms",
            description: "Duration of zombie detection scan");

        // Failed Occurrence Handler
        _failedOccurrencesProcessed = _meter.CreateCounter<long>(
            "milvaion.failed_handler.processed",
            unit: "{occurrence}",
            description: "Total number of failed occurrences processed");

        _failedOccurrencesRetried = _meter.CreateCounter<long>(
            "milvaion.failed_handler.retried",
            unit: "{occurrence}",
            description: "Total number of failed occurrences retried");

        _failedOccurrenceProcessDuration = _meter.CreateHistogram<double>(
            "milvaion.failed_handler.process_duration",
            unit: "ms",
            description: "Duration of failed occurrence processing");

        // General
        _serviceIterations = _meter.CreateCounter<long>(
            "milvaion.background_service.iterations",
            unit: "{iteration}",
            description: "Total number of service loop iterations");

        _serviceErrors = _meter.CreateCounter<long>(
            "milvaion.background_service.errors",
            unit: "{error}",
            description: "Total number of service errors");

        _iterationDuration = _meter.CreateHistogram<double>(
            "milvaion.background_service.iteration_duration",
            unit: "ms",
            description: "Duration of service iterations");
    }

    #region Job Dispatcher

    /// <summary>
    /// Records a successful job dispatch.
    /// </summary>
    public void RecordJobDispatched(string jobType = null, string priority = null)
    {
        var tags = new TagList();
        if (jobType != null) tags.Add("job_type", jobType);
        if (priority != null) tags.Add("priority", priority);
        _jobsDispatched.Add(1, tags);
    }

    /// <summary>
    /// Records multiple job dispatches.
    /// </summary>
    public void RecordJobsDispatched(int count, string jobType = null)
    {
        var tags = new TagList();
        if (jobType != null) tags.Add("job_type", jobType);
        _jobsDispatched.Add(count, tags);
    }

    /// <summary>
    /// Records a job dispatch failure.
    /// </summary>
    public void RecordDispatchFailure(string reason = null)
    {
        var tags = new TagList();
        if (reason != null) tags.Add("reason", reason);
        _jobDispatchFailures.Add(1, tags);
    }

    /// <summary>
    /// Records the duration of a dispatch operation.
    /// </summary>
    public void RecordDispatchDuration(double durationMs, int jobCount = 1)
    {
        _dispatchDuration.Record(durationMs, new TagList { { "job_count", jobCount.ToString() } });
    }

    /// <summary>
    /// Updates the pending jobs count.
    /// </summary>
    public void SetPendingJobsCount(int count) => _pendingJobsCount = count;

    #endregion

    #region Status Tracker

    /// <summary>
    /// Records processed status updates.
    /// </summary>
    public void RecordStatusUpdatesProcessed(int count)
    {
        _statusUpdatesProcessed.Add(count);
    }

    /// <summary>
    /// Records a status update by its final status.
    /// </summary>
    public void RecordStatusUpdateByStatus(string status)
    {
        _statusUpdatesByStatus.Add(1, new TagList { { "status", status } });
    }

    /// <summary>
    /// Records a status update failure.
    /// </summary>
    public void RecordStatusUpdateFailure(string reason = null)
    {
        var tags = new TagList();
        if (reason != null) tags.Add("reason", reason);
        _statusUpdateFailures.Add(1, tags);
    }

    /// <summary>
    /// Records the duration of a status update batch.
    /// </summary>
    public void RecordStatusUpdateDuration(double durationMs, int batchSize)
    {
        _statusUpdateDuration.Record(durationMs, new TagList { { "batch_size", batchSize.ToString() } });
    }

    /// <summary>
    /// Updates the current status batch size.
    /// </summary>
    public void SetStatusBatchSize(int size) => _statusBatchSize = size;

    #endregion

    #region Log Collector

    /// <summary>
    /// Records collected logs.
    /// </summary>
    public void RecordLogsCollected(int count, string logLevel = null)
    {
        var tags = new TagList();
        if (logLevel != null) tags.Add("level", logLevel);
        _logsCollected.Add(count, tags);
    }

    /// <summary>
    /// Records a log collection failure.
    /// </summary>
    public void RecordLogCollectionFailure(string reason = null)
    {
        var tags = new TagList();
        if (reason != null) tags.Add("reason", reason);
        _logCollectionFailures.Add(1, tags);
    }

    /// <summary>
    /// Records the duration of a log batch processing.
    /// </summary>
    public void RecordLogBatchDuration(double durationMs, int batchSize)
    {
        _logBatchDuration.Record(durationMs, new TagList { { "batch_size", batchSize.ToString() } });
    }

    /// <summary>
    /// Updates the current log batch size.
    /// </summary>
    public void SetLogBatchSize(int size) => _logBatchSize = size;

    #endregion

    #region Worker Discovery

    /// <summary>
    /// Records a worker registration.
    /// </summary>
    public void RecordWorkerRegistration(string workerType = null)
    {
        var tags = new TagList();
        if (workerType != null) tags.Add("worker_type", workerType);
        _workerRegistrations.Add(1, tags);
    }

    /// <summary>
    /// Records worker heartbeats.
    /// </summary>
    public void RecordWorkerHeartbeats(int count)
    {
        _workerHeartbeats.Add(count);
    }

    /// <summary>
    /// Records a heartbeat processing failure.
    /// </summary>
    public void RecordHeartbeatFailure(string reason = null)
    {
        var tags = new TagList();
        if (reason != null) tags.Add("reason", reason);
        _workerHeartbeatFailures.Add(1, tags);
    }

    /// <summary>
    /// Records the duration of heartbeat batch processing.
    /// </summary>
    public void RecordHeartbeatProcessDuration(double durationMs, int batchSize)
    {
        _heartbeatProcessDuration.Record(durationMs, new TagList { { "batch_size", batchSize.ToString() } });
    }

    /// <summary>
    /// Updates the active workers count.
    /// </summary>
    public void SetActiveWorkersCount(int count) => _activeWorkersCount = count;

    #endregion

    #region Zombie Detector

    /// <summary>
    /// Records detected zombie occurrences.
    /// </summary>
    public void RecordZombiesDetected(int count)
    {
        _zombieOccurrencesDetected.Add(count);
    }

    /// <summary>
    /// Records recovered zombie occurrences.
    /// </summary>
    public void RecordZombiesRecovered(int count)
    {
        _zombieOccurrencesRecovered.Add(count);
    }

    /// <summary>
    /// Records the duration of a zombie detection scan.
    /// </summary>
    public void RecordZombieDetectionDuration(double durationMs)
    {
        _zombieDetectionDuration.Record(durationMs);
    }

    #endregion

    #region Failed Occurrence Handler

    /// <summary>
    /// Records processed failed occurrences.
    /// </summary>
    public void RecordFailedOccurrencesProcessed(int count)
    {
        _failedOccurrencesProcessed.Add(count);
    }

    /// <summary>
    /// Records retried failed occurrences.
    /// </summary>
    public void RecordFailedOccurrencesRetried(int count)
    {
        _failedOccurrencesRetried.Add(count);
    }

    /// <summary>
    /// Records the duration of failed occurrence processing.
    /// </summary>
    public void RecordFailedOccurrenceProcessDuration(double durationMs)
    {
        _failedOccurrenceProcessDuration.Record(durationMs);
    }

    #endregion

    #region General

    /// <summary>
    /// Records a service iteration.
    /// </summary>
    public void RecordServiceIteration(string serviceName)
    {
        _serviceIterations.Add(1, new TagList { { "service", serviceName } });
    }

    /// <summary>
    /// Records a service error.
    /// </summary>
    public void RecordServiceError(string serviceName, string errorType = null)
    {
        var tags = new TagList { { "service", serviceName } };
        if (errorType != null) tags.Add("error_type", errorType);
        _serviceErrors.Add(1, tags);
    }

    /// <summary>
    /// Records the duration of a service iteration.
    /// </summary>
    public void RecordIterationDuration(string serviceName, double durationMs)
    {
        _iterationDuration.Record(durationMs, new TagList { { "service", serviceName } });
    }

    /// <summary>
    /// Creates a stopwatch for measuring duration, returns an IDisposable that records on dispose.
    /// </summary>
    public DurationRecorder MeasureDuration(string serviceName) => new(this, serviceName);

    #endregion

    /// <inheritdoc/>
    public void Dispose() => _meter?.Dispose();

    /// <summary>
    /// Helper class for measuring duration using the using pattern.
    /// </summary>
    public readonly struct DurationRecorder : IDisposable
    {
        private readonly BackgroundServiceMetrics _metrics;
        private readonly string _serviceName;
        private readonly Stopwatch _stopwatch;

        internal DurationRecorder(BackgroundServiceMetrics metrics, string serviceName)
        {
            _metrics = metrics;
            _serviceName = serviceName;
            _stopwatch = Stopwatch.StartNew();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _stopwatch.Stop();
            _metrics.RecordIterationDuration(_serviceName, _stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}
