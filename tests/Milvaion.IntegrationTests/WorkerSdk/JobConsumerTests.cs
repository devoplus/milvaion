using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Core.Abstractions;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Milvasoft.Milvaion.Sdk.Utils;
using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using Milvasoft.Milvaion.Sdk.Worker.Core;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.WorkerSdk;

/// <summary>
/// Integration tests for JobConsumer.
/// Tests RabbitMQ message consumption, job execution, retry logic, and DLQ routing.
/// </summary>
[Collection(nameof(WorkerSdkTestCollection))]
public class JobConsumerTests(WorkerSdkContainerFixture fixture, ITestOutputHelper output) : WorkerSdkTestBase(fixture, output)
{
    private const string _testRoutingPattern = "test-consumer.*";
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    #region Job Resolution

    [Fact]
    public async Task JobConsumer_ShouldResolveCorrectJobType_WhenMultipleJobsRegistered()
    {
        // Arrange

        var services = new ServiceCollection();
        services.AddTransient<IJobBase, SuccessAsyncJob>();
        services.AddTransient<IJobBase, AnotherAsyncJob>();
        services.AddScoped<IMilvaLogger>(sp => sp.GetRequiredService<ILoggerFactory>().CreateMilvaLogger<IMilvaLogger>());
        services.AddSingleton<ILoggerFactory>(GetLoggerFactory());
        services.AddScoped<JobExecutor>();
        services.AddSingleton<WorkerJobTracker>();

        var sp = services.BuildServiceProvider();

        var jobs = sp.GetServices<IJobBase>().ToList();

        // Assert
        jobs.Should().HaveCount(2);
        jobs.Should().Contain(j => j.GetType().Name == nameof(SuccessAsyncJob));
        jobs.Should().Contain(j => j.GetType().Name == nameof(AnotherAsyncJob));
    }

    #endregion

    #region JobExecutor Integration with JobConsumer Context

    [Fact]
    public async Task JobExecutor_ShouldReturnCompleted_WhenAsyncJobSucceeds()
    {
        // Arrange

        var loggerFactory = GetLoggerFactory();
        var executor = new JobExecutor(loggerFactory);
        var job = new SuccessAsyncJob();
        var correlationId = Guid.CreateVersion7();
        var scheduledJob = CreateScheduledJob(nameof(SuccessAsyncJob));
        var workerOptions = CreateWorkerOptions();
        var config = new JobConsumerConfig { ExecutionTimeoutSeconds = 30 };

        // Act
        var result = await executor.ExecuteAsync(job, scheduledJob, correlationId, null, workerOptions, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(JobOccurrenceStatus.Completed);
        result.CorrelationId.Should().Be(correlationId);
        result.DurationMs.Should().BeGreaterOrEqualTo(0);
        result.Exception.Should().BeNull();
    }

    [Fact]
    public async Task JobExecutor_ShouldReturnFailed_WhenJobThrowsException()
    {
        // Arrange

        var loggerFactory = GetLoggerFactory();
        var executor = new JobExecutor(loggerFactory);
        var job = new FailingAsyncJob();
        var correlationId = Guid.CreateVersion7();
        var scheduledJob = CreateScheduledJob(nameof(FailingAsyncJob));
        var workerOptions = CreateWorkerOptions();
        var config = new JobConsumerConfig { ExecutionTimeoutSeconds = 30 };

        // Act
        var result = await executor.ExecuteAsync(job, scheduledJob, correlationId, null, workerOptions, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(JobOccurrenceStatus.Failed);
        result.Exception.Should().Contain("Simulated job failure");
        result.IsPermanentFailure.Should().BeFalse();
    }

    [Fact]
    public async Task JobExecutor_ShouldReturnTimedOut_WhenJobExceedsTimeout()
    {
        // Arrange

        var loggerFactory = GetLoggerFactory();
        var executor = new JobExecutor(loggerFactory);
        var job = new SlowAsyncJob();
        var correlationId = Guid.CreateVersion7();
        var scheduledJob = CreateScheduledJob(nameof(SlowAsyncJob));
        var workerOptions = CreateWorkerOptions();
        var config = new JobConsumerConfig { ExecutionTimeoutSeconds = 1 }; // 1 second timeout

        // Act
        var result = await executor.ExecuteAsync(job, scheduledJob, correlationId, null, workerOptions, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(JobOccurrenceStatus.TimedOut);
        result.Exception.Should().Contain("timeout");
    }

    [Fact]
    public async Task JobExecutor_ShouldReturnPermanentFailure_WhenPermanentJobExceptionThrown()
    {
        // Arrange

        var loggerFactory = GetLoggerFactory();
        var executor = new JobExecutor(loggerFactory);
        var job = new PermanentFailureJob();
        var correlationId = Guid.CreateVersion7();
        var scheduledJob = CreateScheduledJob(nameof(PermanentFailureJob));
        var workerOptions = CreateWorkerOptions();
        var config = new JobConsumerConfig { ExecutionTimeoutSeconds = 30 };

        // Act
        var result = await executor.ExecuteAsync(job, scheduledJob, correlationId, null, workerOptions, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(JobOccurrenceStatus.Failed);
        result.IsPermanentFailure.Should().BeTrue();
        result.Exception.Should().Contain("Invalid job data");
    }

    [Fact]
    public async Task JobExecutor_ShouldReturnCancelled_WhenCancellationRequested()
    {
        // Arrange

        var loggerFactory = GetLoggerFactory();
        var executor = new JobExecutor(loggerFactory);
        var job = new CancellationAwareJob();
        var correlationId = Guid.CreateVersion7();
        var scheduledJob = CreateScheduledJob(nameof(CancellationAwareJob));
        var workerOptions = CreateWorkerOptions();
        var config = new JobConsumerConfig { ExecutionTimeoutSeconds = 0 }; // No timeout

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var result = await executor.ExecuteAsync(job, scheduledJob, correlationId, null, workerOptions, config, cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(JobOccurrenceStatus.Cancelled);
    }

    [Fact]
    public async Task JobExecutor_ShouldReturnResult_WhenAsyncJobWithResultSucceeds()
    {
        // Arrange

        var loggerFactory = GetLoggerFactory();
        var executor = new JobExecutor(loggerFactory);
        var job = new AsyncJobWithResultImpl();
        var correlationId = Guid.CreateVersion7();
        var scheduledJob = CreateScheduledJob(nameof(AsyncJobWithResultImpl));
        var workerOptions = CreateWorkerOptions();
        var config = new JobConsumerConfig { ExecutionTimeoutSeconds = 30 };

        // Act
        var result = await executor.ExecuteAsync(job, scheduledJob, correlationId, null, workerOptions, config, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Status.Should().Be(JobOccurrenceStatus.Completed);
        result.Result.Should().Be("Custom result from job");
    }

    #endregion

    #region RabbitMQ Message Publishing (simulating dispatcher -> consumer flow)

    [Fact]
    public async Task RabbitMQ_ShouldPublishAndConsumeMessage()
    {
        // Arrange

        var rabbitFactory = new ConnectionFactory
        {
            HostName = GetRabbitMqHost(),
            Port = GetRabbitMqPort(),
            UserName = "guest",
            Password = "guest"
        };

        var exchangeName = WorkerConstant.ExchangeName;
        var routingKey = "test-consumer.testjob";
        var queueName = $"{WorkerConstant.Queues.Jobs}.test-consumer.wildcard";

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await using var connection = await rabbitFactory.CreateConnectionAsync(cts.Token);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cts.Token);

        // Declare exchange and queue
        await channel.ExchangeDeclareAsync(exchange: exchangeName, type: "topic", durable: true, cancellationToken: cts.Token);
        await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, cancellationToken: cts.Token);
        await channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: _testRoutingPattern, cancellationToken: cts.Token);

        // Publish a test job message
        var correlationId = Guid.CreateVersion7();
        var testJob = CreateScheduledJob(nameof(SuccessAsyncJob));
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(testJob));
        var properties = new BasicProperties
        {
            Persistent = true,
            CorrelationId = correlationId.ToString(),
            Headers = new Dictionary<string, object>
            {
                { "CorrelationId", Encoding.UTF8.GetBytes(correlationId.ToString()) }
            }
        };

        await channel.BasicPublishAsync(exchange: exchangeName, routingKey: routingKey, mandatory: false, basicProperties: properties, body: body, cancellationToken: cts.Token);

        // Consume the message
        var messageReceived = false;
        ScheduledJob receivedJob = null;

        var getResult = await channel.BasicGetAsync(queueName, autoAck: true, cts.Token);

        if (getResult != null)
        {
            messageReceived = true;
            receivedJob = JsonSerializer.Deserialize<ScheduledJob>(getResult.Body.Span, _jsonOptions);
        }

        // Assert
        messageReceived.Should().BeTrue("message should be available in queue");
        receivedJob.Should().NotBeNull();
        receivedJob.JobNameInWorker.Should().Be(nameof(SuccessAsyncJob));
    }

    [Fact]
    public async Task RabbitMQ_DLQ_ShouldReceiveNackedMessages()
    {
        // Arrange

        var rabbitFactory = new ConnectionFactory
        {
            HostName = GetRabbitMqHost(),
            Port = GetRabbitMqPort(),
            UserName = "guest",
            Password = "guest"
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        await using var connection = await rabbitFactory.CreateConnectionAsync(cts.Token);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cts.Token);

        // Setup DLX and DLQ
        await channel.ExchangeDeclareAsync(exchange: WorkerConstant.DeadLetterExchangeName, type: "direct", durable: true, cancellationToken: cts.Token);
        await channel.QueueDeclareAsync(queue: WorkerConstant.Queues.FailedOccurrences, durable: true, exclusive: false, autoDelete: false, cancellationToken: cts.Token);
        await channel.QueueBindAsync(queue: WorkerConstant.Queues.FailedOccurrences, exchange: WorkerConstant.DeadLetterExchangeName, routingKey: WorkerConstant.DeadLetterRoutingKey, cancellationToken: cts.Token);

        // Setup main queue with DLX
        var dlqQueueName = "test-dlq-source-queue";
        var queueArgs = new Dictionary<string, object>
        {
            { "x-dead-letter-exchange", WorkerConstant.DeadLetterExchangeName },
            { "x-dead-letter-routing-key", WorkerConstant.DeadLetterRoutingKey }
        };

        await channel.QueueDeclareAsync(queue: dlqQueueName, durable: true, exclusive: false, autoDelete: false, arguments: queueArgs, cancellationToken: cts.Token);

        // Publish a message to the main queue
        var body = Encoding.UTF8.GetBytes("test-dlq-message");
        await channel.BasicPublishAsync(exchange: "", routingKey: dlqQueueName, body: body, cancellationToken: cts.Token);

        // Get and NACK the message (send to DLQ)
        var getResult = await channel.BasicGetAsync(dlqQueueName, autoAck: false, cts.Token);
        getResult.Should().NotBeNull();

        await channel.BasicNackAsync(getResult.DeliveryTag, multiple: false, requeue: false, cts.Token);

        // Wait a moment for DLQ processing
        await Task.Delay(500, cts.Token);

        // Check DLQ
        var dlqResult = await channel.BasicGetAsync(WorkerConstant.Queues.FailedOccurrences, autoAck: true, cts.Token);

        // Assert
        dlqResult.Should().NotBeNull("message should be routed to DLQ after NACK");
    }

    #endregion

    #region WorkerJobTracker Integration

    [Fact]
    public async Task WorkerJobTracker_ShouldTrackConcurrentJobs()
    {
        // Arrange

        var loggerFactory = GetLoggerFactory();
        var tracker = new WorkerJobTracker(loggerFactory);
        var workerId = "integration-test-worker";

        // Act - simulate multiple concurrent jobs
        tracker.IncrementJobCount(workerId);
        tracker.IncrementJobCount(workerId);
        tracker.IncrementJobCount(workerId);

        var peakCount = tracker.GetJobCount(workerId);

        tracker.DecrementJobCount(workerId);
        var afterDecrement = tracker.GetJobCount(workerId);

        // Assert
        peakCount.Should().Be(3);
        afterDecrement.Should().Be(2);
    }

    [Fact]
    public async Task WorkerJobTracker_ShouldTrackMultipleWorkers()
    {
        // Arrange

        var loggerFactory = GetLoggerFactory();
        var tracker = new WorkerJobTracker(loggerFactory);

        // Act
        tracker.IncrementJobCount("worker-1");
        tracker.IncrementJobCount("worker-1");
        tracker.IncrementJobCount("worker-2");

        var allCounts = tracker.GetAllJobCounts();

        // Assert
        allCounts.Should().HaveCount(2);
        allCounts["worker-1"].Should().Be(2);
        allCounts["worker-2"].Should().Be(1);
    }

    #endregion

    #region Helper Methods

    private static ScheduledJob CreateScheduledJob(string jobName) => new()
    {
        Id = Guid.CreateVersion7(),
        DisplayName = $"Test {jobName}",
        JobNameInWorker = jobName,
        ExecuteAt = DateTime.UtcNow,
        IsActive = true
    };

    private WorkerOptions CreateWorkerOptions()
    {
        var options = new WorkerOptions
        {
            WorkerId = "test-worker",
            MaxParallelJobs = 5,
            Heartbeat = new HeartbeatSettings { JobHeartbeatIntervalSeconds = 0 },
            RabbitMQ = new RabbitMQSettings
            {
                Host = GetRabbitMqHost(),
                Port = GetRabbitMqPort(),
                Username = "guest",
                Password = "guest",
                VirtualHost = "/",
                RoutingKeyPattern = _testRoutingPattern
            }
        };

        options.RegenerateInstanceId();

        return options;
    }

    #endregion

    #region Test Job Implementations

    private sealed class SuccessAsyncJob : IAsyncJob
    {
        public Task ExecuteAsync(IJobContext context)
        {
            context.Log("Job executed successfully");
            return Task.CompletedTask;
        }
    }

    private sealed class AnotherAsyncJob : IAsyncJob
    {
        public Task ExecuteAsync(IJobContext context) => Task.CompletedTask;
    }

    private sealed class FailingAsyncJob : IAsyncJob
    {
        public Task ExecuteAsync(IJobContext context) => throw new InvalidOperationException("Simulated job failure");
    }

    private sealed class SlowAsyncJob : IAsyncJob
    {
        public Task ExecuteAsync(IJobContext context) => Task.Delay(TimeSpan.FromSeconds(30), context.CancellationToken);
    }

    private sealed class PermanentFailureJob : IAsyncJob
    {
        public Task ExecuteAsync(IJobContext context) => throw new Milvasoft.Milvaion.Sdk.Worker.Exceptions.PermanentJobException("Invalid job data");
    }

    private sealed class CancellationAwareJob : IAsyncJob
    {
        public Task ExecuteAsync(IJobContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            return Task.Delay(TimeSpan.FromSeconds(60), context.CancellationToken);
        }
    }

    private sealed class AsyncJobWithResultImpl : IAsyncJobWithResult
    {
        public Task<string> ExecuteAsync(IJobContext context) => Task.FromResult("Custom result from job");
    }

    #endregion
}
