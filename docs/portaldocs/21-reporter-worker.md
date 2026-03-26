---
id: reporter-worker
title: Reporter Worker
sidebar_position: 21
description: Pre-built Reporter Worker that generates automated metric reports for job performance, worker throughput, and workflow health analytics.
---

The Reporter Worker is a built-in analytics worker that automatically generates metric reports about your Milvaion infrastructure. It queries `JobOccurrences`, `ScheduledJobs`, `WorkflowRuns`, and `Workflows` tables using Dapper and writes aggregated JSON reports to the `MetricReports` table for consumption by the API and Dashboard UI.

### Features

- 10 built-in metric report types (job, worker, and workflow analytics)
- Direct PostgreSQL access via Dapper (high-performance, no EF overhead)
- Configurable lookback window and data retention
- JSONB report storage for efficient querying
- Time-series, ranking, and health score reports
- Automatic period tracking (PeriodStartTime / PeriodEndTime)
- Structured logging with Serilog (console + Seq)

### Use Cases

| Scenario | Example |
|----------|---------|
| **Failure Monitoring** | Track error rate trends across all jobs over time |
| **Performance Analysis** | Identify slowest jobs with P50/P95/P99 duration metrics |
| **Worker Capacity Planning** | Monitor throughput and utilization per worker instance |
| **SLA Compliance** | Measure schedule deviation between cron and actual execution |
| **Job Health Tracking** | Score each job by success rate for reliability dashboards |
| **Workflow Analytics** | Analyze workflow success rates and step-level bottlenecks |

### Report Types

The Reporter Worker includes 10 report jobs organized into three categories:

#### Job Metrics

| Job Class | Metric Type | Description |
|-----------|-------------|-------------|
| `FailureRateTrendReportJob` | `FailureRateTrend` | Hourly failure rate percentage over the lookback period |
| `PercentileDurationsReportJob` | `PercentileDurations` | P50/P95/P99 execution duration distribution per job |
| `TopSlowJobsReportJob` | `TopSlowJobs` | Jobs ranked by highest average execution duration |
| `JobHealthScoreReportJob` | `JobHealthScore` | Success rate and occurrence counts per job |
| `CronScheduleVsActualReportJob` | `CronScheduleVsActual` | Deviation between scheduled and actual execution times |

#### Worker Metrics

| Job Class | Metric Type | Description |
|-----------|-------------|-------------|
| `WorkerThroughputReportJob` | `WorkerThroughput` | Job count, success/failure breakdown per worker |
| `WorkerUtilizationTrendReportJob` | `WorkerUtilizationTrend` | Hourly capacity utilization percentage per worker |

#### Workflow Metrics

| Job Class | Metric Type | Description |
|-----------|-------------|-------------|
| `WorkflowSuccessRateReportJob` | `WorkflowSuccessRate` | Success, failure, partial, and cancelled rates per workflow |
| `WorkflowStepBottleneckReportJob` | `WorkflowStepBottleneck` | Step-level avg/max duration, failure count, and retry count |
| `WorkflowDurationTrendReportJob` | `WorkflowDurationTrend` | Average workflow execution duration over time |

### Worker Configuration

Configure the Reporter Worker in `appsettings.json`:

```json
{
  "Reporter": {
    "DatabaseConnectionString": "Host=localhost;Port=5432;Database=MilvaionDb;Username=postgres;Password=secret;",
    "ReportGeneration": {
      "DataRetentionDays": 30,
      "LookbackHours": 24,
      "TopNLimit": 10,
      "MaxScheduleDeviations": 500
    }
  }
}
```

#### Configuration Properties

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `DatabaseConnectionString` | string | ✓ | - | PostgreSQL connection string for reading occurrence/workflow data and writing reports |
| `DataRetentionDays` | int | - | `30` | How many days of historical data to consider |
| `LookbackHours` | int | - | `24` | Time window (in hours) for time-series reports |
| `TopNLimit` | int | - | `10` | Maximum items in ranking reports (TopSlowJobs) |
| `MaxScheduleDeviations` | int | - | `500` | Maximum deviation records for CronScheduleVsActual |

### Scheduling Report Jobs

Each report type is an independent job. Schedule them through the Milvaion API just like any other worker job:

```json
{
  "displayName": "Failure Rate Trend Report",
  "selectedJobName": "FailureRateTrendReportJob",
  "cronExpression": "0 */6 * * *",
  "isActive": true
}
```

#### Recommended Schedules

| Job | Cron Expression | Frequency | Rationale |
|-----|-----------------|-----------|-----------|
| `FailureRateTrendReportJob` | `0 */6 * * *` | Every 6 hours | Track error trends throughout the day |
| `PercentileDurationsReportJob` | `0 */6 * * *` | Every 6 hours | Monitor latency distribution changes |
| `TopSlowJobsReportJob` | `0 0 * * *` | Daily at midnight | Daily ranking is sufficient |
| `WorkerThroughputReportJob` | `0 */6 * * *` | Every 6 hours | Track worker load throughout the day |
| `WorkerUtilizationTrendReportJob` | `0 */6 * * *` | Every 6 hours | Capacity monitoring |
| `CronScheduleVsActualReportJob` | `0 0 * * *` | Daily at midnight | Accumulated daily deviations |
| `JobHealthScoreReportJob` | `0 0 * * *` | Daily at midnight | Daily health overview |
| `WorkflowSuccessRateReportJob` | `0 0 * * *` | Daily at midnight | Daily workflow health |
| `WorkflowStepBottleneckReportJob` | `0 0 * * *` | Daily at midnight | Daily step analysis |
| `WorkflowDurationTrendReportJob` | `0 */6 * * *` | Every 6 hours | Track workflow duration trends |

### How Reports Are Generated

All report jobs follow the same pattern:

1. **Open a direct PostgreSQL connection** using `DatabaseConnectionString`
2. **Execute an aggregation SQL query** with the configured lookback window (`LookbackHours`)
3. **Map results** into a strongly-typed data model
4. **Serialize to JSON** and insert into the `MetricReports` table via Dapper
5. **Return a success result** with the new report ID and summary counts

```
┌─────────────────────────────────────────────┐
│          ReporterWorker Job Execution        │
│                                             │
│  1. NpgsqlConnection.OpenAsync()            │
│  2. SELECT ... FROM "JobOccurrences"        │
│     WHERE "StartTime" >= @PeriodStart       │
│  3. Map to data model (e.g. TopSlowJobsData)│
│  4. JsonSerializer.Serialize(data)          │
│  5. INSERT INTO "MetricReports" (jsonb)     │
│  6. Return { Success, ReportId, Count }     │
└─────────────────────────────────────────────┘
```

> **Note:** The Reporter Worker uses direct Dapper queries instead of EF Core for better performance on aggregation queries. It reads from the same PostgreSQL database that the Milvaion API uses.

### Report Data Schemas

Each metric type stores its result as a JSON payload in the `Data` column (PostgreSQL `jsonb`).

#### FailureRateTrend

Hourly error rate as a time series with a configurable threshold.

```json
{
  "thresholdPercentage": 5.0,
  "dataPoints": [
    { "timestamp": "2026-06-01T10:00:00Z", "value": 2.5 },
    { "timestamp": "2026-06-01T11:00:00Z", "value": 3.1 }
  ]
}
```

#### PercentileDurations

P50/P95/P99 duration distribution per job (requires ≥10 occurrences).

```json
{
  "jobs": {
    "EmailSenderJob": { "p50": 120.5, "p95": 450.2, "p99": 890.7 },
    "DataSyncJob": { "p50": 80.3, "p95": 310.1, "p99": 620.4 }
  }
}
```

#### TopSlowJobs

Jobs ranked by average duration, limited by `TopNLimit`.

```json
{
  "jobs": [
    { "jobName": "HeavyReportJob", "averageDurationMs": 45200.5, "occurrenceCount": 12 },
    { "jobName": "DataMigrationJob", "averageDurationMs": 32100.3, "occurrenceCount": 8 }
  ]
}
```

#### WorkerThroughput

Per-worker job count and success/failure breakdown.

```json
{
  "workers": [
    {
      "workerId": "worker-1",
      "jobCount": 150,
      "successCount": 145,
      "failureCount": 5,
      "averageDurationMs": 1200.5
    }
  ]
}
```

#### WorkerUtilizationTrend

Hourly utilization percentage per worker (capped at 100%).

```json
{
  "dataPoints": [
    {
      "timestamp": "2026-06-01T10:00:00Z",
      "workerUtilization": { "worker-1": 75.5, "worker-2": 42.3 }
    }
  ]
}
```

#### CronScheduleVsActual

Deviation between cron-scheduled and actual execution times, sorted by largest deviation.

```json
{
  "jobs": [
    {
      "occurrenceId": "01968a3b-...",
      "jobId": "01968a2a-...",
      "jobName": "HourlySync",
      "scheduledTime": "2026-06-01T10:00:00Z",
      "actualTime": "2026-06-01T10:00:12Z",
      "deviationSeconds": 12.0
    }
  ]
}
```

#### JobHealthScore

Success rate per job (requires ≥5 occurrences), ordered by lowest success rate.

```json
{
  "jobs": [
    {
      "jobName": "EmailSenderJob",
      "successRate": 98.5,
      "totalOccurrences": 200,
      "successCount": 197,
      "failureCount": 3
    }
  ]
}
```

#### WorkflowSuccessRate

Per-workflow success/failure/partial/cancelled breakdown.

```json
{
  "workflows": [
    {
      "workflowId": "01968a3b-...",
      "workflowName": "OrderProcessing",
      "successRate": 95.0,
      "totalRuns": 100,
      "completedCount": 95,
      "failedCount": 3,
      "partialCount": 1,
      "cancelledCount": 1,
      "avgDurationMs": 5400.0
    }
  ]
}
```

#### WorkflowStepBottleneck

Step-level performance analysis per workflow.

```json
{
  "workflows": [
    {
      "workflowId": "01968a3b-...",
      "workflowName": "OrderProcessing",
      "steps": [
        {
          "stepName": "ValidateOrder",
          "avgDurationMs": 200.5,
          "maxDurationMs": 1500.0,
          "executionCount": 100,
          "failureCount": 2,
          "skippedCount": 0,
          "retryCount": 1
        }
      ]
    }
  ]
}
```

#### WorkflowDurationTrend

Average workflow duration over time.

```json
{
  "dataPoints": [
    {
      "timestamp": "2026-06-01T10:00:00Z",
      "workflowAvgDurationMs": {
        "OrderProcessing": 5200.0,
        "DataPipeline": 12400.0
      }
    }
  ]
}
```

### Deployment

The Reporter Worker can be deployed as a Docker container:

```yaml
# docker-compose.yml
services:
  reporter-worker:
    image: milvasoft/milvaion-reporter-worker:latest
    environment:
      - Worker__WorkerId=reporter-worker-01
      - Worker__RabbitMQ__Host=rabbitmq
      - Worker__RabbitMQ__Port=5672
      - Worker__RabbitMQ__Username=guest
      - Worker__RabbitMQ__Password=guest
      - Worker__MaxParallelJobs=4
      - Reporter__DatabaseConnectionString=Host=postgres;Port=5432;Database=MilvaionDb;Username=postgres;Password=secret
      - Reporter__ReportGeneration__LookbackHours=24
      - Reporter__ReportGeneration__TopNLimit=10
    depends_on:
      - postgres
      - rabbitmq
    restart: unless-stopped
```

#### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: reporter-worker
spec:
  replicas: 1
  selector:
    matchLabels:
      app: reporter-worker
  template:
    metadata:
      labels:
        app: reporter-worker
    spec:
      containers:
        - name: reporter-worker
          image: milvasoft/milvaion-reporter-worker:latest
          env:
            - name: Worker__WorkerId
              value: "reporter-worker-01"
            - name: Worker__RabbitMQ__Host
              value: "rabbitmq"
            - name: Reporter__DatabaseConnectionString
              valueFrom:
                secretKeyRef:
                  name: milvaion-secrets
                  key: database-connection-string
            - name: Reporter__ReportGeneration__LookbackHours
              value: "24"
          resources:
            requests:
              memory: "128Mi"
              cpu: "100m"
            limits:
              memory: "512Mi"
              cpu: "500m"
```

> **Note:** A single replica is typically sufficient since report generation jobs run periodically (every 6 hours or daily), not continuously. Scale only if you have very high report frequency requirements.

### Best Practices

1. **Schedule Reports During Low Traffic**
   - Run daily reports (TopSlowJobs, JobHealthScore, CronScheduleVsActual) at off-peak hours
   - Time-series reports (FailureRateTrend, WorkerUtilizationTrend) can run every 6 hours safely

2. **Tune the Lookback Window**
   - Default `LookbackHours: 24` covers a full day
   - For high-volume environments, consider shorter windows (6–12 hours) to reduce query load
   - For low-volume environments, extend to 48–72 hours for more meaningful data

3. **Set Appropriate TopN Limits**
   - Default `TopNLimit: 10` works well for most deployments
   - Increase for environments with many different job types

4. **Implement Data Retention**
   - Reports accumulate over time—use the cleanup API (`DELETE /metricreports/cleanup?OlderThanDays=30`)
   - Schedule a periodic cleanup job via the Maintenance Worker or a cron-based scheduled job

5. **Monitor Report Generation**
   - Check Serilog output for generation success/failure messages
   - Each job logs the number of data points or items generated
   - Failed report generation does **not** affect other reports (jobs are independent)

6. **Use Read Replicas for Heavy Workloads**
   - Point `DatabaseConnectionString` to a PostgreSQL read replica to avoid impacting the primary database
   - Especially important for PercentileDurations and WorkerUtilizationTrend which run aggregation-heavy queries

### Troubleshooting

#### Reports Not Being Generated

1. **Check worker connectivity**: Ensure the worker can reach RabbitMQ and PostgreSQL
2. **Check job schedules**: Verify report jobs are scheduled and active in the Milvaion API
3. **Check logs**: Look for `Starting ... Report generation` log entries

#### Reports Show Empty Data

1. **Check lookback window**: If `LookbackHours` is 24 but no jobs ran in the last 24 hours, data will be empty
2. **Check minimum thresholds**: PercentileDurations requires ≥10 occurrences, JobHealthScore requires ≥5 occurrences per job
3. **Check workflow data**: Workflow reports require `WorkflowRuns` records

#### Database Connection Errors

```
Npgsql.NpgsqlException: Failed to connect to ...
```

- Verify `DatabaseConnectionString` is correct
- Check network connectivity between the worker and PostgreSQL
- Ensure the database user has SELECT permission on `JobOccurrences`, `ScheduledJobs`, `WorkflowRuns`, `Workflows` and INSERT permission on `MetricReports`

#### High Query Load

- Reduce `LookbackHours` to narrow the query window
- Schedule reports less frequently (e.g., daily instead of every 6 hours)
- Point `DatabaseConnectionString` to a read replica
- Add appropriate indexes on `JobOccurrences.StartTime` and `WorkflowRuns.StartTime`

---

*For viewing and managing generated reports via the API and Dashboard, see [Enterprise Features — Metric Reports](20-enterprise-features.md#metric-reports). For custom workers, see [Your First Worker](04-your-first-worker.md) and [Implementing Jobs](05-implementing-jobs.md).*
