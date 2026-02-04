---
id: external-scheduler
title: External Scheduler Integration
sidebar_position: 18
description: Integrate Quartz.NET, Hangfire, or other external schedulers with Milvaion for unified job monitoring.
---

Milvaion supports integration with external schedulers like **Quartz.NET** and **Hangfire**. This allows you to keep your existing scheduler while gaining Milvaion's powerful monitoring, dashboards, and alerting capabilities.

### Why External Scheduler Integration?

| Scenario | Solution |
|----------|----------|
| **Existing Quartz/Hangfire Infrastructure** | Keep your scheduler, add Milvaion monitoring |
| **Complex Scheduling Needs** | Use Quartz's advanced features (calendars, clustering) |
| **Gradual Migration** | Start with monitoring, migrate jobs later |
| **Multi-Scheduler Environment** | Monitor all schedulers in one dashboard |
| **Regulatory Requirements** | Use battle-tested schedulers for critical jobs |

### How It Works

```
┌─────────────────────────────────────────────────────────────────┐
│                     Your Application                             │
├─────────────────────────────────────────────────────────────────┤
│  ┌─────────────────┐    ┌─────────────────────────────────────┐ │
│  │   Quartz.NET    │───▶│   Milvaion.Sdk.Worker.Quartz        │ │
│  │   Scheduler     │    │   ┌───────────────────────────────┐ │ │
│  │                 │    │   │ MilvaionJobListener           │ │ │
│  │ ┌─────────────┐ │    │   │ • JobToBeExecuted → Starting  │ │ │
│  │ │ Your Jobs   │ │    │   │ • JobWasExecuted → Completed  │ │ │
│  │ └─────────────┘ │    │   └───────────────────────────────┘ │ │
│  └─────────────────┘    └──────────────┬──────────────────────┘ │
└────────────────────────────────────────┼────────────────────────┘
                                         │ RabbitMQ
                                         ▼
┌─────────────────────────────────────────────────────────────────┐
│                     Milvaion Server                              │
├─────────────────────────────────────────────────────────────────┤
│  ┌───────────────────────────────────────────────────────────┐  │
│  │ ExternalJobTrackerService                                  │  │
│  │ • Creates ScheduledJob records (IsExternal = true)         │  │
│  │ • Creates JobOccurrence records for each execution         │  │
│  │ • Updates status, duration, result, exception              │  │
│  └───────────────────────────────────────────────────────────┘  │
│                              ▼                                   │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐          │
│  │  Dashboard   │  │   Alerts     │  │   Metrics    │          │
│  │  • Job List  │  │  • Failures  │  │  • EPM       │          │
│  │  • History   │  │  • Timeouts  │  │  • Duration  │          │
│  └──────────────┘  └──────────────┘  └──────────────┘          │
└─────────────────────────────────────────────────────────────────┘
```

### Supported Schedulers

| Scheduler | SDK Package | Status |
|-----------|-------------|--------|
| **Quartz.NET** | `Milvasoft.Milvaion.Sdk.Worker.Quartz` | ✅ Available |
| **Hangfire** | `Milvasoft.Milvaion.Sdk.Worker.Hangfire` | 🚧 Coming Soon |

---

## Quartz.NET Integration

### Quick Start

#### 1. Add NuGet Package

```bash
dotnet add package Milvasoft.Milvaion.Sdk.Worker.Quartz
```

#### 2. Configure Services

```csharp
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Extensions;
using Quartz;

var builder = Host.CreateApplicationBuilder(args);

// Add Milvaion Quartz integration
// This registers core worker services AND Quartz listeners
builder.Services.AddMilvaionQuartzIntegration(builder.Configuration);

// Configure Quartz with your jobs
builder.Services.AddQuartz(q =>
{
    // Enable Milvaion listeners for job monitoring
    q.UseMilvaion(builder.Services);

    // Register your jobs
    var myJobKey = new JobKey("MyJob", "MyGroup");
    q.AddJob<MyJob>(opts => opts
        .WithIdentity(myJobKey)
        .WithDescription("My scheduled job"));

    q.AddTrigger(opts => opts
        .ForJob(myJobKey)
        .WithIdentity("MyJob-Trigger")
        .WithCronSchedule("0 0 * * * ?") // Every hour
        .WithDescription("Runs MyJob every hour"));
});

// Add Quartz hosted service
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

await builder.Build().RunAsync();
```

#### 3. Configure appsettings.json

```json
{
  "Worker": {
    "WorkerId": "quartz-worker",
    "MaxParallelJobs": 128,
    "RabbitMQ": {
      "Host": "rabbitmq",
      "Port": 5672,
      "Username": "guest",
      "Password": "guest",
      "VirtualHost": "/"
    },
    "Redis": {
      "ConnectionString": "redis:6379"
    },
    "Heartbeat": {
      "Enabled": true,
      "IntervalSeconds": 5
    },
    "ExternalScheduler": {
      "Source": "Quartz"
    }
  }
}
```

### Implementing Jobs with Milvaion Integration

Your Quartz jobs can publish logs to Milvaion for real-time monitoring:

```csharp
using Microsoft.Extensions.Logging;
using Milvasoft.Milvaion.Sdk.Worker.RabbitMQ;
using Quartz;

public class MyJob(ILogger<MyJob> logger, ILogPublisher logPublisher) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var dataMap = context.MergedJobDataMap;
        
        // Get Milvaion tracking info (set by MilvaionJobListener)
        var correlationId = Guid.Parse(dataMap.GetString("Milvaion_CorrelationId") ?? Guid.Empty.ToString());
        var workerId = dataMap.GetString("Milvaion_WorkerId") ?? "quartz-worker";

        // Publish log to Milvaion
        await logPublisher.PublishLogAsync(new JobOccurrenceLogMessage
        {
            CorrelationId = correlationId,
            WorkerId = workerId,
            Level = LogLevel.Information.ToString(),
            Message = "Starting job execution...",
            Timestamp = DateTime.UtcNow
        });

        // Your job logic here
        await DoWorkAsync();

        // Publish completion log
        await logPublisher.PublishLogAsync(new JobOccurrenceLogMessage
        {
            CorrelationId = correlationId,
            WorkerId = workerId,
            Level = LogLevel.Information.ToString(),
            Message = "Job completed successfully",
            Timestamp = DateTime.UtcNow,
            Data = new Dictionary<string, object> { ["result"] = "success" }
        });

        // Flush logs before job completes
        await logPublisher.FlushAsync();
        
        // Set result (optional)
        context.Result = JsonSerializer.Serialize(new { Status = "Completed" });
    }
}
```

---

## Using the Pre-built Quartz Worker Docker Image

For quick testing or production deployments, use the pre-built Quartz worker image:

### Docker Run

```bash
docker run -d \
  --name quartz-worker \
  -e Worker__RabbitMQ__Host=rabbitmq \
  -e Worker__RabbitMQ__Port=5672 \
  -e Worker__RabbitMQ__Username=guest \
  -e Worker__RabbitMQ__Password=guest \
  -e Worker__Redis__ConnectionString=redis:6379 \
  milvasoft/milvaion-quartz-worker:latest
```

### Docker Compose

```yaml
services:
  quartz-worker:
    image: milvasoft/milvaion-quartz-worker:latest
    container_name: quartz-worker
    restart: unless-stopped
    environment:
      - Worker__WorkerId=quartz-worker-1
      - Worker__RabbitMQ__Host=rabbitmq
      - Worker__RabbitMQ__Port=5672
      - Worker__RabbitMQ__Username=guest
      - Worker__RabbitMQ__Password=guest
      - Worker__Redis__ConnectionString=redis:6379
      - Worker__ExternalScheduler__Source=Quartz
    networks:
      - milvaion-network
    depends_on:
      - rabbitmq
      - redis

  # Add more workers for scaling
  quartz-worker-2:
    image: milvasoft/milvaion-quartz-worker:latest
    container_name: quartz-worker-2
    restart: unless-stopped
    environment:
      - Worker__WorkerId=quartz-worker-2
      - Worker__RabbitMQ__Host=rabbitmq
      # ... same config
```

### Kubernetes Deployment

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: quartz-worker
spec:
  replicas: 3
  selector:
    matchLabels:
      app: quartz-worker
  template:
    metadata:
      labels:
        app: quartz-worker
    spec:
      containers:
        - name: quartz-worker
          image: milvasoft/milvaion-quartz-worker:latest
          env:
            - name: Worker__WorkerId
              valueFrom:
                fieldRef:
                  fieldPath: metadata.name
            - name: Worker__RabbitMQ__Host
              valueFrom:
                configMapKeyRef:
                  name: milvaion-config
                  key: rabbitmq-host
            - name: Worker__RabbitMQ__Username
              valueFrom:
                secretKeyRef:
                  name: rabbitmq-credentials
                  key: username
            - name: Worker__RabbitMQ__Password
              valueFrom:
                secretKeyRef:
                  name: rabbitmq-credentials
                  key: password
            - name: Worker__Redis__ConnectionString
              valueFrom:
                configMapKeyRef:
                  name: milvaion-config
                  key: redis-connection
          resources:
            requests:
              memory: "256Mi"
              cpu: "250m"
            limits:
              memory: "512Mi"
              cpu: "500m"
```

---

## Dashboard Behavior for External Jobs

External jobs appear in the Milvaion dashboard with special indicators and restricted actions:

### Job List View

| Feature | Internal Jobs | External Jobs |
|---------|---------------|---------------|
| **Badge** | - | 🟣 "External" badge |
| **Trigger Button** | ✅ Enabled | 🔒 Disabled |
| **Delete Button** | ✅ Enabled | 🔒 Disabled |
| **Edit Button** | ✅ Full edit | ⚠️ Limited edit |

### Job Detail View

External jobs show additional information:

```
┌─────────────────────────────────────────────────────────────────┐
│ 📧 Send Email Job                                               │
│ ✅ Active   🟣 External Job                                     │
│ External ID: Samples.SendEmailJob                               │
├─────────────────────────────────────────────────────────────────┤
│ [Trigger Now]  [Edit Job]  [Delete]                             │
│    🔒 Disabled                🔒 Disabled                       │
└─────────────────────────────────────────────────────────────────┘
```

### Job Edit Form

When editing external jobs, the following fields are **disabled**:

| Field | Editable | Reason |
|-------|----------|--------|
| Display Name | ✅ Yes | Milvaion-only setting |
| Description | ✅ Yes | Milvaion-only setting |
| Tags | ✅ Yes | Milvaion-only setting |
| Zombie Timeout | ✅ Yes | Milvaion zombie detection |
| **Worker** | 🔒 No | Managed by Quartz |
| **Job Type** | 🔒 No | Managed by Quartz |
| **Schedule (Cron)** | 🔒 No | Managed by Quartz |
| **Job Data** | 🔒 No | Managed by Quartz |
| **Execution Timeout** | 🔒 No | Managed by Quartz |
| **Concurrent Policy** | 🔒 No | Managed by Quartz |
| **Active Status** | 🔒 No | Managed by Quartz |
| **Auto-Disable** | 🔒 No | Not applicable |

### Occurrence Detail View

External job occurrences show the external job ID:

```
EXTERNAL JOB: Samples.SendEmailJob
```

The **Cancel** button is disabled for external job occurrences since Milvaion cannot cancel jobs running in Quartz.

---

## Configuration Reference

### Worker Configuration

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `WorkerId` | string | ✅ | - | Unique identifier for this worker |
| `MaxParallelJobs` | int | - | `10` | Maximum concurrent job executions |
| `RabbitMQ` | object | ✅ | - | RabbitMQ connection settings |
| `Redis` | object | ✅ | - | Redis connection settings |
| `Heartbeat` | object | - | - | Heartbeat configuration |
| `ExternalScheduler` | object | - | - | External scheduler settings |

### ExternalScheduler Configuration

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Source` | string | ✅ | - | Scheduler identifier (e.g., "Quartz", "Hangfire") |

---

## How External Jobs Are Tracked

### Job Registration

When Quartz starts, `MilvaionSchedulerListener` registers all jobs:

```
Quartz → ExternalJobRegistrationMessage → RabbitMQ → ExternalJobTrackerService
                                                              ↓
                                                    Creates ScheduledJob
                                                    (IsExternal = true)
```

### Job Execution

When a job runs, `MilvaionJobListener` tracks the execution:

```
JobToBeExecuted:
Quartz → ExternalJobOccurrenceMessage (Starting) → RabbitMQ → ExternalJobTrackerService
                                                                      ↓
                                                            Creates JobOccurrence
                                                            (Status = Running)

JobWasExecuted:
Quartz → ExternalJobOccurrenceMessage (Completed/Failed) → RabbitMQ → ExternalJobTrackerService
                                                                              ↓
                                                                    Updates JobOccurrence
                                                                    (Status, Duration, Result)
```

### Logs

Jobs can publish logs via `ILogPublisher`:

```
Job → JobOccurrenceLogMessage → RabbitMQ → LogConsumerService
                                                   ↓
                                         Adds to JobOccurrence.Logs
                                         Publishes via SignalR
```

---

## Metrics and Monitoring

External jobs contribute to all Milvaion metrics:

| Metric | External Jobs Included |
|--------|------------------------|
| **Executions Per Minute (EPM)** | ✅ Yes |
| **Average Duration** | ✅ Yes |
| **Total Occurrences** | ✅ Yes |
| **Status Counters** (Running/Completed/Failed) | ✅ Yes |
| **Job Success Rate** | ✅ Yes |

---

## Troubleshooting

### Jobs Not Appearing in Dashboard

1. **Check RabbitMQ Connection**
   ```bash
   docker logs quartz-worker | grep -i "connected to rabbitmq"
   ```

2. **Check ExternalJobTrackerService**
   ```bash
   docker logs milvaion-api | grep -i "external"
   ```

3. **Verify Configuration**
   - Ensure `ExternalScheduler.Source` is set
   - Check RabbitMQ credentials

### Occurrences Not Updating

1. **Check Correlation ID**
   - Ensure `Milvaion_CorrelationId` is in JobDataMap

2. **Check Listener Registration**
   - Verify `q.UseMilvaion(builder.Services)` is called

### Logs Not Appearing

1. **Check LogPublisher**
   - Ensure `ILogPublisher` is injected
   - Call `FlushAsync()` before job completes

2. **Check CorrelationId**
   - Logs require valid `CorrelationId`
