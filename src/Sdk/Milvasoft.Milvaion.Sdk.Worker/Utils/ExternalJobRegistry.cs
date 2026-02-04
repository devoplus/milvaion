using Milvasoft.Milvaion.Sdk.Worker.Options;

namespace Milvasoft.Milvaion.Sdk.Worker.Utils;

/// <summary>
/// Registry that collects external job configurations for WorkerListenerPublisher.
/// </summary>
public class ExternalJobRegistry
{
    private readonly Dictionary<string, JobConsumerConfig> _jobConfigs = [];
    private readonly Lock _lock = new();

    /// <summary>
    /// Registers a external job as a JobConsumerConfig.
    /// </summary>
    public void RegisterJob(string externalJobId, Type jobType)
    {
        lock (_lock)
        {
            if (_jobConfigs.ContainsKey(externalJobId))
                return;

            var config = new JobConsumerConfig
            {
                ConsumerId = externalJobId,
                RoutingPattern = "external.job.*",
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
