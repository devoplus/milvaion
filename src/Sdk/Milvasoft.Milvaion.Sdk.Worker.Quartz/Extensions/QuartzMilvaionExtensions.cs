using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Listeners;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Services;
using Milvasoft.Milvaion.Sdk.Worker.Utils;
using Quartz;

namespace Milvasoft.Milvaion.Sdk.Worker.Quartz.Extensions;

/// <summary>
/// Extension methods for configuring Milvaion Quartz integration.
/// </summary>
public static class QuartzMilvaionExtensions
{
    /// <summary>
    /// Adds Milvaion integration services for Quartz.
    /// This registers the publisher and listeners needed to report Quartz jobs to Milvaion.
    /// Also registers core worker services (heartbeat, status updates, etc.) without JobConsumer.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddMilvaionQuartzIntegration(this IServiceCollection services, IConfiguration configuration)
    {
        // 1. Register core worker services WITHOUT JobConsumer/JobExecutor
        services.AddMilvaionWorkerCore(configuration, requireJobConsumers: false);

        // 2. Bind Quartz-specific options
        var options = new MilvaionExternalSchedulerOptions();

        configuration.GetSection(MilvaionExternalSchedulerOptions.SectionKey).Bind(options);

        services.AddSingleton(options);

        // 3. Register ExternalJobPublisher for publishing job events to RabbitMQ
        services.AddSingleton<IExternalJobPublisher>(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();
            return new ExternalJobPublisher(sp.GetService<IOptions<WorkerOptions>>(), loggerFactory);
        });

        // 4. Register listeners (will be added to Quartz via UseMilvaion)
        services.AddSingleton<MilvaionJobListener>();
        services.AddSingleton<MilvaionSchedulerListener>();

        // 5. Register ExternalJobRegistry for collecting job configs
        services.AddSingleton<ExternalJobRegistry>();

        Console.WriteLine("[Quartz] Milvaion Quartz integration registered. Jobs will be discovered at startup.");

        return services;
    }

    /// <summary>
    /// Configures Quartz to use Milvaion integration listeners.
    /// Call this inside AddQuartz() configuration.
    /// </summary>
    /// <param name="quartz">The Quartz service collection configurator.</param>
    /// <param name="services">The service collection (for future extensibility).</param>
    /// <returns>The Quartz configurator for chaining.</returns>
    public static IServiceCollectionQuartzConfigurator UseMilvaion(this IServiceCollectionQuartzConfigurator quartz)
    {
        quartz.AddJobListener<MilvaionJobListener>();
        quartz.AddSchedulerListener<MilvaionSchedulerListener>();

        return quartz;
    }

    /// <summary>
    /// Generates a consistent external job ID from the Quartz JobKey.
    /// </summary>
    public static string GetExternalJobId(this JobKey jobKey) => jobKey.ToString();
}
