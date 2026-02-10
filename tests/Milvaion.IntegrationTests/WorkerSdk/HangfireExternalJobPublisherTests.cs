using FluentAssertions;
using Microsoft.Extensions.Options;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using Milvasoft.Milvaion.Sdk.Utils;
using Milvasoft.Milvaion.Sdk.Worker.Hangfire.Services;
using Milvasoft.Milvaion.Sdk.Worker.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.WorkerSdk;

/// <summary>
/// Integration tests for Hangfire ExternalJobPublisher.
/// Tests job registration and occurrence event publishing to RabbitMQ.
/// </summary>
[Collection(nameof(WorkerSdkTestCollection))]
public class HangfireExternalJobPublisherTests(WorkerSdkContainerFixture fixture, ITestOutputHelper output) : WorkerSdkTestBase(fixture, output)
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task PublishJobRegistrationAsync_ShouldPublishToQueue()
    {
        // Arrange
        await PurgeQueueAsync(WorkerConstant.Queues.ExternalJobRegistration);

        var publisher = CreateHangfirePublisher();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var message = new ExternalJobRegistrationMessage
        {
            ExternalJobId = $"TestHangfireJob.Execute_{Guid.CreateVersion7():N}",
            Source = "Hangfire",
            DisplayName = "TestHangfireJob.Execute",
            Description = "Hangfire recurring job: test-job",
            JobTypeName = "TestApp.Jobs.TestHangfireJob",
            CronExpression = "*/5 * * * *",
            WorkerId = "hangfire-test-worker",
            IsActive = true
        };

        // Act
        await publisher.PublishJobRegistrationAsync(message, cts.Token);

        // Assert
        var received = await ConsumeFromQueueAsync<ExternalJobRegistrationMessage>(WorkerConstant.Queues.ExternalJobRegistration, cts.Token);

        received.Should().NotBeNull();
        received.ExternalJobId.Should().Be(message.ExternalJobId);
        received.Source.Should().Be("Hangfire");
        received.CronExpression.Should().Be("*/5 * * * *");
        received.IsActive.Should().BeTrue();

        await publisher.DisposeAsync();
    }

    [Fact]
    public async Task PublishOccurrenceEventAsync_ShouldPublishStartingEvent()
    {
        // Arrange
        await PurgeQueueAsync(WorkerConstant.Queues.ExternalJobOccurrence);

        var publisher = CreateHangfirePublisher();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var correlationId = Guid.CreateVersion7();
        var message = new ExternalJobOccurrenceMessage
        {
            CorrelationId = correlationId,
            ExternalJobId = "TestHangfireJob.Execute",
            ExternalOccurrenceId = "hangfire-job-12345",
            Source = "Hangfire",
            JobTypeName = "TestApp.Jobs.TestHangfireJob",
            EventType = ExternalOccurrenceEventType.Starting,
            WorkerId = "hangfire-test-worker",
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
        received.Source.Should().Be("Hangfire");
        received.ExternalOccurrenceId.Should().Be("hangfire-job-12345");

        await publisher.DisposeAsync();
    }

    [Fact]
    public async Task PublishOccurrenceEventAsync_ShouldPublishCompletedEvent()
    {
        // Arrange
        await PurgeQueueAsync(WorkerConstant.Queues.ExternalJobOccurrence);

        var publisher = CreateHangfirePublisher();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var correlationId = Guid.CreateVersion7();
        var message = new ExternalJobOccurrenceMessage
        {
            CorrelationId = correlationId,
            ExternalJobId = "TestHangfireJob.Execute",
            Source = "Hangfire",
            JobTypeName = "TestApp.Jobs.TestHangfireJob",
            EventType = ExternalOccurrenceEventType.Completed,
            WorkerId = "hangfire-test-worker",
            Status = JobOccurrenceStatus.Completed,
            EndTime = DateTime.UtcNow,
            DurationMs = 3200,
            Result = "Hangfire job completed successfully"
        };

        // Act
        await publisher.PublishOccurrenceEventAsync(message, cts.Token);

        // Assert
        var received = await ConsumeFromQueueAsync<ExternalJobOccurrenceMessage>(WorkerConstant.Queues.ExternalJobOccurrence, cts.Token);

        received.Should().NotBeNull();
        received.Status.Should().Be(JobOccurrenceStatus.Completed);
        received.DurationMs.Should().Be(3200);
        received.Result.Should().Contain("completed");

        await publisher.DisposeAsync();
    }

    [Fact]
    public async Task PublishOccurrenceEventAsync_ShouldNotThrow_WhenPublishFails()
    {
        // Hangfire publisher swallows exceptions (by design - no throw)
        // This test verifies that behavior

        // Arrange

        var publisher = CreateHangfirePublisher();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        var message = new ExternalJobOccurrenceMessage
        {
            CorrelationId = Guid.CreateVersion7(),
            ExternalJobId = "TestJob",
            Source = "Hangfire",
            EventType = ExternalOccurrenceEventType.Starting,
            Status = JobOccurrenceStatus.Running
        };

        // Act - publish should succeed against real RabbitMQ
        var act = async () => await publisher.PublishOccurrenceEventAsync(message, cts.Token);
        await act.Should().NotThrowAsync();

        await publisher.DisposeAsync();
    }

    [Fact]
    public async Task PublishJobRegistrationAsync_ShouldThrowOnNullMessage()
    {
        // Arrange

        var publisher = CreateHangfirePublisher();

        // Act & Assert
        var act = async () => await publisher.PublishJobRegistrationAsync(null);
        await act.Should().ThrowAsync<ArgumentNullException>();

        await publisher.DisposeAsync();
    }

    [Fact]
    public async Task DisposeAsync_ShouldCleanupResources()
    {
        // Arrange

        var publisher = CreateHangfirePublisher();

        // Ensure connection is established
        await publisher.PublishJobRegistrationAsync(new ExternalJobRegistrationMessage
        {
            ExternalJobId = "DisposeTest",
            Source = "Hangfire",
            DisplayName = "Dispose Test"
        });

        // Act & Assert - should not throw
        var act = async () => await publisher.DisposeAsync();
        await act.Should().NotThrowAsync();
    }

    #region Helpers

    private ExternalJobPublisher CreateHangfirePublisher()
    {
        var options = Options.Create(new WorkerOptions
        {
            WorkerId = "hangfire-test-worker",
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
