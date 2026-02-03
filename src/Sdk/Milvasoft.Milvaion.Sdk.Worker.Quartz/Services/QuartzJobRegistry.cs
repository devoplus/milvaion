using Milvasoft.Milvaion.Sdk.Worker.Options;

namespace Milvasoft.Milvaion.Sdk.Worker.Quartz.Services;

/// <summary>
/// Registry that collects Quartz job configurations for WorkerListenerPublisher.
/// </summary>
public class QuartzJobRegistry
{
    private readonly Dictionary<string, JobConsumerConfig> _jobConfigs = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Registers a Quartz job as a JobConsumerConfig.
    /// </summary>
    public void RegisterJob(string jobName, string jobGroup, Type jobType)
    {
        lock (_lock)
        {
            var externalJobId = string.IsNullOrEmpty(jobGroup) || jobGroup == "DEFAULT"
                ? jobName
                : $"{jobGroup}.{jobName}";

            var config = new JobConsumerConfig
            {
                ConsumerId = externalJobId,
                RoutingPattern = $"quartz.{externalJobId.ToLowerInvariant()}.*",
                MaxParallelJobs = 1,
                JobType = jobType
            };

            _jobConfigs[externalJobId] = config;
        }
    }

    /// <summary>
    /// Gets all registered job configurations.
    /// </summary>
    public Dictionary<string, JobConsumerConfig> GetJobConfigs()
    {
        lock (_lock)
        {
            return new Dictionary<string, JobConsumerConfig>(_jobConfigs);
        }
    }

    /// <summary>
    /// Gets the count of registered jobs.
    /// </summary>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _jobConfigs.Count;
            }
        }
    }
}