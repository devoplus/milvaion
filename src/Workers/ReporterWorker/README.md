# Reporter Worker

ReporterWorker is a background worker application that regularly collects metrics and performance reports from the Milvaion job scheduler and saves them to MilvaionDb.

## Features

ReporterWorker generates the following metric reports:

### 1. Failure Rate Trend
- **Description**: Changes in error rate over time
- **Visualization**: Line chart + threshold line
- **Usage**: Monitor system health and detect failure trends

### 2. P50 / P95 / P99 Durations
- **Description**: Percentile-based duration distribution
- **Visualization**: Box plot or multi-line chart
- **Usage**: Performance analysis and outlier detection

### 3. Top Slow Jobs
- **Description**: Average duration by job name
- **Visualization**: Horizontal bar chart
- **Usage**: Identify slow jobs for performance optimization

### 4. Worker Throughput
- **Description**: Number of jobs processed by each worker over time
- **Visualization**: Grouped bar chart
- **Usage**: Analyze worker load distribution

### 5. Worker Utilization Trend
- **Description**: Capacity vs actual utilization rate (time series)
- **Visualization**: Multi-line chart / Heatmap
- **Usage**: Monitor worker capacity and load distribution

### 6. Cron Schedule vs Actual
- **Description**: Deviation between scheduled and actual execution times
- **Visualization**: Scatter plot / Table
- **Usage**: Analyze scheduling accuracy

### 7. Job Health Score
- **Description**: Success rate for last N executions per job
- **Visualization**: Scoreboard / gauge
- **Usage**: Monitor job reliability

## Configuration

### appsettings.json

```json
{
  "Reporter": {
    "DatabaseConnectionString": "Host=postgres;Port=5432;Database=MilvaionDb;...",
    "ReportGeneration": {
      "DataRetentionDays": 30,      // Report retention period
      "LookbackHours": 24,           // Analysis window (hours)
      "TopNLimit": 10                // Limit for Top N lists
    }
  }
}
```

## Scheduling Jobs

You need to schedule these jobs from Milvaion UI to generate reports:

1. **FailureRateTrendReportJob**
   - Recommended: Hourly
   - Cron: `0 * * * *`
   - Job Data: `{}`

2. **PercentileDurationsReportJob**
   - Recommended: Every 6 hours
   - Cron: `0 */6 * * *`
   - Job Data: `{}`

3. **TopSlowJobsReportJob**
   - Recommended: Twice daily
   - Cron: `0 8,20 * * *`
   - Job Data: `{}`

4. **WorkerThroughputReportJob**
   - Recommended: Every 4 hours
   - Cron: `0 */4 * * *`
   - Job Data: `{}`

5. **WorkerUtilizationTrendReportJob**
   - Recommended: Every 2 hours
   - Cron: `0 */2 * * *`
   - Job Data: `{}`

6. **CronScheduleVsActualReportJob**
   - Recommended: Every 6 hours
   - Cron: `0 */6 * * *`
   - Job Data: `{}`

7. **JobHealthScoreReportJob**
   - Recommended: Daily at 9 AM
   - Cron: `0 9 * * *`
   - Job Data: `{}`

### Quick Setup via UI

1. Navigate to **Jobs** page
2. Click **New Job**
3. Fill in:
   - **Display Name**: (e.g., "Failure Rate Trend Report")
   - **Job Type**: Select the job from dropdown (e.g., `FailureRateTrendReportJob`)
   - **Cron Expression**: Use recommended cron above
   - **Job Data**: `{}`
   - **Tags**: `reporter` (optional)
4. Click **Save**
5. Repeat for all 7 jobs

## Database

Reports are stored in the `MetricReports` table:

- **Id**: Unique identifier (Guid v7)
- **MetricType**: Report type (FailureRateTrend, PercentileDurations, etc.)
- **DisplayName**: Display name for UI
- **Data**: Report data in JSON format (jsonb)
- **PeriodStartTime**: Analysis period start time
- **PeriodEndTime**: Analysis period end time
- **GeneratedAt**: Report generation timestamp

## Development

To add a new metric report:

1. Add the new metric type to `Models/MetricTypes.cs`
2. Add the data model to `Models/MetricDataModels.cs`
3. Add a new job class to the `Jobs/` folder (implement `IAsyncJobWithResult<string>`)
4. Add the job consumer configuration to `appsettings.json`

## Dependencies

- Milvasoft.Milvaion.Sdk.Worker
- Milvaion.Infrastructure (for MilvaionDbContext access)
- Dapper (for SQL queries)
- Npgsql (for PostgreSQL connection)

## Running the Worker

```bash
dotnet run
```

Or with Docker:

```bash
docker build -t milvaion-reporterworker .
docker run -d --name reporter-worker milvaion-reporterworker
```

Or with default Milvaion Network:

```bash
docker run --name reporter-worker --network milvaion_milvaion-network milvaion-reporterworker
```
