using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Extensions;
using Quartz;
using SampleQuartzWorker.Jobs;

// Build host
var builder = Host.CreateApplicationBuilder(args);

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// Add Milvaion Quartz integration
// This registers all core worker services (heartbeat, status updates, etc.)
// AND Quartz-specific listeners for job monitoring
builder.Services.AddMilvaionQuartzIntegration(builder.Configuration);

// Configure Quartz with sample jobs
builder.Services.AddQuartz(q =>
{
    // Use Milvaion listeners for job monitoring
    q.UseMilvaion(builder.Services);

    // Register SampleLogJob - runs every 30 seconds
    var sampleLogJobKey = new JobKey("SampleLogJob", "Samples");
    q.AddJob<SampleLogJob>(opts => opts
        .WithIdentity(sampleLogJobKey)
        .WithDescription("Sample job that logs messages periodically"));

    q.AddTrigger(opts => opts
        .ForJob(sampleLogJobKey)
        .WithIdentity("SampleLogJob-Trigger")
        .WithCronSchedule("0/30 * * * * ?") // Every 30 seconds
        .WithDescription("Triggers SampleLogJob every 30 seconds"));

    // Register SendEmailJob - runs every minute
    var sendEmailJobKey = new JobKey("SendEmailJob", "Samples");
    q.AddJob<SendEmailJob>(opts => opts
        .WithIdentity(sendEmailJobKey)
        .WithDescription("Sample job that simulates sending emails")
        .UsingJobData("Recipient", "test@example.com")
        .UsingJobData("Subject", "Scheduled Notification"));

    q.AddTrigger(opts => opts
        .ForJob(sendEmailJobKey)
        .WithIdentity("SendEmailJob-Trigger")
        .WithCronSchedule("0/10 * * * * ?") // Every 10 sec
        .WithDescription("Triggers SendEmailJob every minute"));
});

// Add Quartz hosted service
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);

// Build and run
var host = builder.Build();

await host.RunAsync();
