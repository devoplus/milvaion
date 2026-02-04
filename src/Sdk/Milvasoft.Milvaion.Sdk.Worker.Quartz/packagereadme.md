# Milvaion Quartz Integration

[![license](https://img.shields.io/badge/license-MIT-blue.svg)](https://github.com/Milvasoft/Milvaion/blob/master/LICENSE) 
[![NuGet](https://img.shields.io/nuget/v/Milvasoft.Milvaion.Sdk.Worker.Quartz)](https://www.nuget.org/packages/Milvasoft.Milvaion.Sdk.Worker.Quartz/)   
[![NuGet](https://img.shields.io/nuget/dt/Milvasoft.Milvaion.Sdk.Worker.Quartz)](https://www.nuget.org/packages/Milvasoft.Milvaion.Sdk.Worker.Quartz/)

**Milvaion Quartz Integration** allows you to monitor your existing Quartz.NET jobs through Milvaion's dashboard without changing your scheduler. Keep using Quartz for scheduling while gaining Milvaion's monitoring, alerting, and metrics capabilities.

---

## Features

- **Zero-Code Job Changes** - Your existing Quartz jobs work as-is
- **Automatic Job Registration** - Jobs are automatically registered with Milvaion when scheduled
- **Real-time Monitoring** - Track job executions, durations, and failures in Milvaion dashboard
- **Execution Tracking** - Every job execution is recorded with status, duration, and result
- **Log Publishing** - Publish job logs to Milvaion for real-time viewing
- **Worker Heartbeats** - Automatic health monitoring and status updates
- **Metrics Integration** - Jobs contribute to EPM, average duration, and success rate metrics

---

## Installation

```bash
dotnet add package Milvasoft.Milvaion.Sdk.Worker.Quartz
```

---

## Quick Start

### 1. Configure Services

```csharp
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Extensions;
using Quartz;

var builder = Host.CreateApplicationBuilder(args);

// Add Milvaion Quartz integration
builder.Services.AddMilvaionQuartzIntegration(builder.Configuration);

// Configure Quartz with your jobs
builder.Services.AddQuartz(q =>
{
    // Enable Milvaion listeners
    q.UseMilvaion(builder.Services);

    // Register your jobs as usual
    var jobKey = new JobKey("MyJob", "MyGroup");
    q.AddJob<MyJob>(opts => opts.WithIdentity(jobKey));
    q.AddTrigger(opts => opts
        .ForJob(jobKey)
        .WithCronSchedule("0 0 * * * ?"));
});

builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

await builder.Build().RunAsync();
```

### 2. Configure appsettings.json

```json
{
  "Worker": {
    "WorkerId": "quartz-worker-1",
    "RabbitMQ": {
      "Host": "localhost",
      "Port": 5672,
      "Username": "guest",
      "Password": "guest"
    },
    "Redis": {
      "ConnectionString": "localhost:6379"
    },
    "ExternalScheduler": {
      "SourceName": "Quartz"
    }
  }
}
```

### 3. (Optional) Publish Logs from Jobs

```csharp
public class MyJob(ILogPublisher logPublisher) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var correlationId = Guid.Parse(
            context.MergedJobDataMap.GetString("Milvaion_CorrelationId"));
        var workerId = context.MergedJobDataMap.GetString("Milvaion_WorkerId");

        await logPublisher.PublishLogAsync(new JobOccurrenceLogMessage
        {
            CorrelationId = correlationId,
            WorkerId = workerId,
            Level = "Information",
            Message = "Processing started...",
            Timestamp = DateTime.UtcNow
        });

        // Your job logic here

        await logPublisher.FlushAsync();
    }
}
```

---

## Dashboard Features

External jobs appear in Milvaion dashboard with:

- Full execution history
- Real-time logs (if published)
- Duration and success rate metrics

---

## Documentation

For complete documentation, visit: [Milvaion Documentation](https://github.com/Milvasoft/milvaion)

## License

This project is licensed under the MIT License.
