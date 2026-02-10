using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using Milvasoft.Milvaion.Sdk.Utils;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using Milvasoft.Milvaion.Sdk.Worker.Quartz.Services;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.WorkerSdk;

/// <summary>
/// Integration tests for Quartz ExternalJobPublisher.
/// Tests job registration and occurrence event publishing to RabbitMQ.
/// </summary>
[Collection(nameof(WorkerSdkTestCollection))]
public class QuartzExternalJobPublisherTests(WorkerSdkContainerFixture fixture, ITestOutputHelper output) : WorkerSdkTestBase(fixture, output)
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task PublishJobRegistrationAsync_ShouldPublishToQueue()
    {
        // Arrange
        await PurgeQueueAsync(WorkerConstant.Queues.ExternalJobRegistration);

        var publisher = CreateQuartzPublisher();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var message = new ExternalJobRegistrationMessage
        {
            ExternalJobId = $"DEFAULT.TestQuartzJob_{Guid.CreateVersion7():N}",
            Source = "Quartz",
            DisplayName = "Test Quartz Job",
            Description = "Integration test job",
            JobTypeName = "TestApp.Jobs.TestQuartzJob",
            CronExpression = "0 * * * * ?",
            WorkerId = "quartz-test-worker",
            IsActive = true
        };

        // Act
        await publisher.PublishJobRegistrationAsync(message, cts.Token);

        // Assert - consume from queue and verify
        var received = await ConsumeFromQueueAsync<ExternalJobRegistrationMessage>(WorkerConstant.Queues.ExternalJobRegistration, cts.Token);

        received.Should().NotBeNull();
        received.ExternalJobId.Should().Be(message.ExternalJobId);
        received.Source.Should().Be("Quartz");
        received.DisplayName.Should().Be("Test Quartz Job");
        received.IsActive.Should().BeTrue();

        await publisher.DisposeAsync();
    }

    [Fact]
    public async Task PublishOccurrenceEventAsync_ShouldPublishStartingEvent()
    {
        // Arrange
        await PurgeQueueAsync(WorkerConstant.Queues.ExternalJobOccurrence);

        var publisher = CreateQuartzPublisher();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var correlationId = Guid.CreateVersion7();
        var message = new ExternalJobOccurrenceMessage
        {
            CorrelationId = correlationId,
            ExternalJobId = "DEFAULT.TestQuartzJob",
            ExternalOccurrenceId = Guid.CreateVersion7().ToString(),
            Source = "Quartz",
            JobTypeName = "TestApp.Jobs.TestQuartzJob",
            EventType = ExternalOccurrenceEventType.Starting,
            WorkerId = "quartz-test-worker",
            Status = JobOccurrenceStatus.Running,
            StartTime = DateTime.UtcNow
        };

        // Act
        await publisher.PublishOccurrenceEventAsync(message, cts.Token);

        // Assert
        var received = await ConsumeFromQueueAsync<ExternalJobOccurrenceMessage>(WorkerConstant.Queues.ExternalJobOccurrence, cts.Token);

        received.Should().NotBeNull();
        received.CorrelationId.Should().Be(correlationId);
        received.Status.Should().Be(JobOccurrenceStatus.Running);
        received.EventType.Should().Be(ExternalOccurrenceEventType.Starting);
        received.Source.Should().Be("Quartz");

        await publisher.DisposeAsync();
    }

    [Fact]
    public async Task PublishOccurrenceEventAsync_ShouldPublishCompletedEvent()
    {
        // Arrange
        await PurgeQueueAsync(WorkerConstant.Queues.ExternalJobOccurrence);

        var publisher = CreateQuartzPublisher();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var correlationId = Guid.CreateVersion7();
        var message = new ExternalJobOccurrenceMessage
        {
            CorrelationId = correlationId,
            ExternalJobId = "DEFAULT.TestQuartzJob",
            Source = "Quartz",
            JobTypeName = "TestApp.Jobs.TestQuartzJob",
            EventType = ExternalOccurrenceEventType.Completed,
            WorkerId = "quartz-test-worker",
            Status = JobOccurrenceStatus.Completed,
            EndTime = DateTime.UtcNow,
            DurationMs = 1500,
            Result = "Job completed successfully"
        };

        // Act
        await publisher.PublishOccurrenceEventAsync(message, cts.Token);

        // Assert
        var received = await ConsumeFromQueueAsync<ExternalJobOccurrenceMessage>(WorkerConstant.Queues.ExternalJobOccurrence, cts.Token);

        received.Should().NotBeNull();
        received.CorrelationId.Should().Be(correlationId);
        received.Status.Should().Be(JobOccurrenceStatus.Completed);
        received.DurationMs.Should().Be(1500);
        received.Result.Should().Contain("completed");

        await publisher.DisposeAsync();
    }

    [Fact]
    public async Task PublishOccurrenceEventAsync_ShouldPublishFailedEvent()
    {
        // Arrange
        await PurgeQueueAsync(WorkerConstant.Queues.ExternalJobOccurrence);

        var publisher = CreateQuartzPublisher();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var correlationId = Guid.CreateVersion7();
        var message = new ExternalJobOccurrenceMessage
        {
            CorrelationId = correlationId,
            ExternalJobId = "DEFAULT.TestQuartzJob",
            Source = "Quartz",
            JobTypeName = "TestApp.Jobs.TestQuartzJob",
            EventType = ExternalOccurrenceEventType.Completed,
            WorkerId = "quartz-test-worker",
            Status = JobOccurrenceStatus.Failed,
            EndTime = DateTime.UtcNow,
            DurationMs = 500,
            Exception = "Test exception message"
        };

        // Act
        await publisher.PublishOccurrenceEventAsync(message, cts.Token);

        // Assert
        var received = await ConsumeFromQueueAsync<ExternalJobOccurrenceMessage>(WorkerConstant.Queues.ExternalJobOccurrence, cts.Token);

        received.Should().NotBeNull();
        received.Status.Should().Be(JobOccurrenceStatus.Failed);
        received.Exception.Should().Contain("Test exception");

        await publisher.DisposeAsync();
    }

    [Fact]
    public async Task PublishJobRegistrationAsync_ShouldThrowOnNullMessage()
    {
        // Arrange

        var publisher = CreateQuartzPublisher();

        // Act & Assert
        var act = async () => await publisher.PublishJobRegistrationAsync(null);
        await act.Should().ThrowAsync<ArgumentNullException>();

        await publisher.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_ShouldCleanupResources()
    {
        // Arrange

        var publisher = CreateQuartzPublisher();

        // Ensure connection is established
        var message = new ExternalJobRegistrationMessage
        {
            ExternalJobId = "DisposeTest",
            Source = "Quartz",
            DisplayName = "Dispose Test"
        };

        await publisher.PublishJobRegistrationAsync(message);

        // Act & Assert - should not throw
        var act = async () => await publisher.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    #region Helpers

    private ExternalJobPublisher CreateQuartzPublisher()
    {
        var options = Options.Create(new WorkerOptions
        {
            WorkerId = "quartz-test-worker",
            RabbitMQ = new RabbitMQSettings
            {
                Host = GetRabbitMqHost(),
                Port = GetRabbitMqPort(),
                Username = "guest",
                Password = "guest",
                VirtualHost = "/"
            }
        });

        return new ExternalJobPublisher(options, GetLoggerFactory());
    }

    private async Task PurgeQueueAsync(string queueName)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = GetRabbitMqHost(),
                Port = GetRabbitMqPort(),
                UserName = "guest",
                Password = "guest"
            };

            await using var connection = await factory.CreateConnectionAsync();
            await using var channel = await connection.CreateChannelAsync();

            await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);
            await channel.QueuePurgeAsync(queueName);
        }
        catch
        {
            // Ignore purge errors
        }
    }

    private async Task<T> ConsumeFromQueueAsync<T>(string queueName, CancellationToken cancellationToken) where T : class
    {
        var factory = new ConnectionFactory
        {
            HostName = GetRabbitMqHost(),
            Port = GetRabbitMqPort(),
            UserName = "guest",
            Password = "guest"
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Wait for message to appear
        var deadline = DateTime.UtcNow.AddSeconds(5);

        while (DateTime.UtcNow < deadline)
        {
            var result = await channel.BasicGetAsync(queueName, autoAck: true, cancellationToken);

            if (result != null)
            {
                var json = Encoding.UTF8.GetString(result.Body.ToArray());
                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }

            await Task.Delay(200, cancellationToken);
        }

        return null;
    }

    #endregion
}
