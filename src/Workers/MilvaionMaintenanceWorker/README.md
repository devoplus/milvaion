# Milvaion Maintenance Worker

Built-in maintenance worker for Milvaion scheduler. Performs database cleanup, archiving, and optimization tasks.

## Jobs

### 1. OccurrenceRetentionJob
Deletes old job occurrences based on retention policy.

**Schedule:** Daily at 2 AM

**Configuration:**

```json
"OccurrenceRetention": {
  "CompletedRetentionDays": 30,
  "FailedRetentionDays": 30,
  "CancelledRetentionDays": 30,
  "TimedOutRetentionDays": 30,
  "BatchSize": 10000,
  "VacuumAfterCleanup": true,
  "VacuumThreshold": 10000
}
```

### 2. OccurrenceArchiveJob
Archives old occurrences to dated tables instead of deleting.

**Schedule:** Monthly (1st day at 4 AM)

**Configuration:**
```json
"OccurrenceArchive": {
  "ArchiveAfterDays": 30,
  "ArchiveTablePrefix": "JobOccurrences_Archive",
  "StatusesToArchive": [ 2, 3, 4, 5 ],
  "BatchSize": 10000,
  "CreateIndexOnArchive": false,
  "VacuumAfterArchive": true,
  "VacuumThreshold": 10000
}
```

**Creates tables:**
- `JobOccurrences_Archive_YYYY_MM`
- `JobOccurrences_Archive_YYYY_MM_Logs`

### 3. DatabaseMaintenanceJob
Runs VACUUM, ANALYZE, and optionally REINDEX on tables.

**Schedule:** Weekly (Sunday at 3 AM)

**Configuration:**
```json
"DatabaseMaintenance": {
  "EnableVacuum": true,
  "EnableAnalyze": true,
  "EnableReindex": false,
  "Tables": [
    "JobOccurrences",
    "JobOccurrenceLogs",
    "ScheduledJobs",
    "FailedOccurrences"
  ]
}
```

### 4. FailedOccurrenceCleanupJob
Cleans up old DLQ (Dead Letter Queue) entries.

**Schedule:** Weekly

**Configuration:**
```json
"FailedOccurrenceRetention": {
  "RetentionDays": 30,
  "BatchSize": 10000
}
```

### 5. RedisCleanupJob
Removes orphaned cache entries and stale locks.

**Schedule:** Daily

**Configuration:**
```json
"RedisCleanup": {
  "KeyPrefix": "Milvaion:JobScheduler:",
  "CleanOrphanedJobCache": true,
  "CleanStaleLocks": true,
  "CleanOrphanedRunningStates": true,
  "StaleLockHours": 24
}
```

### 6. ActivityLogCleanupJob
Deletes old activity logs.

**Configuration:**
```json
"ActivityLogRetention": {
  "RetentionDays": 30
}
```

### 7. NotificationCleanupJob
Deletes old seen/unseen notifications.

**Configuration:**
```json
"NotificationRetention": {
  "SeenRetentionDays": 30,
  "UnseenRetentionDays": 60
}
```

### 8. WorkflowRunRetentionJob
Deletes old workflow runs based on retention policy.

**Schedule:** Daily at 2:30 AM

**Configuration:**
```json
"WorkflowRunRetention": {
  "CompletedRetentionDays": 30,
  "FailedRetentionDays": 90,
  "CancelledRetentionDays": 30,
  "PartiallyCompletedRetentionDays": 60,
  "BatchSize": 1000,
  "VacuumAfterCleanup": true,
  "VacuumThreshold": 1000
}
```

## Running

### Docker
```bash
docker run -d --name maintenance-worker \
  --network milvaion_milvaion-network \
  milvaion-maintenance-worker
```

### Development
```bash
cd src/Workers/MilvaionMaintenanceWorker
dotnet run
```

## Features

- Batch deletions to avoid long locks
- Optional VACUUM after cleanup (immediate space reclaim)
- Separate archive tables for historical data
- JobOccurrenceLogs handled separately
- Configurable retention policies per status
- Non-blocking error handling

## Documentation

- [Maintenance Guide](../../docs/portaldocs/11-maintenance.md)
- [Milvaion Documentation](https://github.com/Milvasoft/milvaion)
