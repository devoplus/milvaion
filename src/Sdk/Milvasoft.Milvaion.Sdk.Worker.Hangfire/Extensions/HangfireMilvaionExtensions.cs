using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvasoft.Milvaion.Sdk.Worker.Hangfire.Filters;
using Milvasoft.Milvaion.Sdk.Worker.Hangfire.Services;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using Milvasoft.Milvaion.Sdk.Worker.Utils;

namespace Milvasoft.Milvaion.Sdk.Worker.Hangfire.Extensions;

/// <summary>
/// Extension methods for configuring Milvaion Hangfire integration.
/// </summary>
public static class HangfireMilvaionExtensions
{
    /// <summary>
    /// Adds Milvaion integration services for Hangfire.
    /// This registers the publisher and filters needed to report Hangfire jobs to Milvaion.
    /// Also registers core worker services (heartbeat, status updates, etc.) without JobConsumer.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMilvaionHangfireIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Register core worker services WITHOUT JobConsumer/JobExecutor
        services.AddMilvaionWorkerCore(configuration, requireJobConsumers: false);

        // 2. Bind Hangfire-specific options
        var options = new MilvaionExternalSchedulerOptions();

        configuration.GetSection(MilvaionExternalSchedulerOptions.SectionKey).Bind(options);

        services.AddSingleton(options);

        // 3. Register ExternalJobPublisher for publishing job events to RabbitMQ
        services.AddSingleton<IExternalJobPublisher>(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();
            return new ExternalJobPublisher(sp.GetService<IOptions<WorkerOptions>>(), loggerFactory);
        });

        // 4. Register ExternalJobRegistry for collecting job configs
        services.AddSingleton<ExternalJobRegistry>();

        // 5. Register MilvaionJobFilter
        services.AddSingleton<MilvaionJobFilter>();

        // 6. Register startup service for worker registration
        services.AddHostedService<HangfireWorkerStartupService>();

        Console.WriteLine("[Hangfire] Milvaion Hangfire integration registered. Jobs will be discovered at startup.");

        return services;
    }

    /// <summary>
    /// Configures Hangfire to use Milvaion integration filter.
    /// Call this after UseHangfireServer() or in Hangfire configuration.
    /// </summary>
    /// <param name="configuration">The Hangfire global configuration.</param>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>The Hangfire configuration for chaining.</returns>
    public static IGlobalConfiguration UseMilvaion(this IGlobalConfiguration configuration, IServiceProvider serviceProvider)
    {
        var filter = serviceProvider.GetRequiredService<MilvaionJobFilter>();

        GlobalJobFilters.Filters.Add(filter);

        return configuration;
    }

    /// <summary>
    /// Generates a consistent external job ID from the Hangfire job type and method.
    /// </summary>
    public static string GetExternalJobId(this Type jobType, string methodName) => $"{jobType.Name}.{methodName}";
}
