using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Milvaion.Application.Utils.Constants;
using Milvaion.Application.Utils.Models.Options;
using Milvaion.Infrastructure.BackgroundServices;
using Milvaion.Infrastructure.Services.RabbitMQ;
using Milvaion.Infrastructure.Telemetry;
using Milvaion.IntegrationTests.TestBase;
using Milvasoft.Milvaion.Sdk.Domain.Enums;
using Milvasoft.Milvaion.Sdk.Domain.JsonModels;
using Milvasoft.Milvaion.Sdk.Utils;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace Milvaion.IntegrationTests.BackgroundServices;

/// <summary>
/// Integration tests for LogCollectorService.
/// Tests log message consumption from RabbitMQ and batch processing.
/// </summary>
[Collection(nameof(ServicesTestCollection))]
public class LogCollectorServiceTests(ServicesWebApplicationFactory factory, ITestOutputHelper output) : BackgroundServiceTestBase(factory, output)
{
    [Fact]
    public async Task CollectLogs_ShouldAppendLogsToOccurrence()
    {
        // Arrange
        await InitializeAsync();
        await PurgeAllQueuesAsync();

        var job = await SeedScheduledJobAsync("LogTestJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Unique message to avoid cross-test interference
        var uniqueMessage = $"Test log message from worker {Guid.CreateVersion7():N}";

        var logMessage = new WorkerLogBatchMessage
        {
            Logs =
            [
                new() {
                    CorrelationId = occurrence.CorrelationId,
                    WorkerId = "test-worker",
                    Log = new OccurrenceLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = "Information",
                        Message = uniqueMessage,
                        Category = "TestCategory"
                    },
                    MessageTimestamp = DateTime.UtcNow
                }
            ],
            BatchTimestamp = DateTime.UtcNow
        };

        // Act - Start the collector first
        var collector = CreateLogCollectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await collector.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        // Give collector time to start
        await Task.Delay(3000, cts.Token);

        // Publish log message
        await PublishLogMessageAsync(logMessage, cts.Token);

        // Wait for condition with polling
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Logs?.Any(l => l.Message == uniqueMessage) == true;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await collector.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("log message should be appended to occurrence");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        var workerLog = updatedOccurrence.Logs.FirstOrDefault(l => l.Message == uniqueMessage);
        workerLog.Should().NotBeNull();
        workerLog!.Level.Should().Be("Information");
        workerLog.Category.Should().Be("TestCategory");
    }

    [Fact]
    public async Task CollectLogs_ShouldBatchProcessMultipleLogs()
    {
        // Arrange
        await InitializeAsync();
        await PurgeAllQueuesAsync();

        var job = await SeedScheduledJobAsync("BatchLogJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        // Unique category to avoid cross-test interference
        var uniqueCategory = $"BatchTest_{Guid.CreateVersion7():N}";

        // Act - Start the collector first
        var collector = CreateLogCollectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await collector.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(3000, cts.Token);

        var logBatchMessage = new WorkerLogBatchMessage
        {
            Logs = [],
            BatchTimestamp = DateTime.UtcNow
        };

        // Publish multiple log messages
        for (int i = 0; i < 5; i++)
        {
            var logMessage = new WorkerLogMessage
            {
                CorrelationId = occurrence.CorrelationId,
                WorkerId = "test-worker",
                Log = new OccurrenceLog
                {
                    Timestamp = DateTime.UtcNow,
                    Level = "Information",
                    Message = $"Batch log message {i}",
                    Category = uniqueCategory
                },
                MessageTimestamp = DateTime.UtcNow
            };

            logBatchMessage.Logs.Add(logMessage);
        }

        await PublishLogMessageAsync(logBatchMessage, cts.Token);

        // Wait for all 5 logs
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Logs?.Count(l => l.Category == uniqueCategory) >= 5;
            },
            timeout: TimeSpan.FromSeconds(20),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await collector.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("all 5 batch logs should be processed");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        var batchLogs = updatedOccurrence.Logs.Where(l => l.Category == uniqueCategory).ToList();
        batchLogs.Should().HaveCount(5);

        for (int i = 0; i < 5; i++)
        {
            batchLogs.Should().Contain(l => l.Message == $"Batch log message {i}");
        }
    }

    [Fact]
    public async Task CollectLogs_ShouldHandleMultipleOccurrences()
    {
        // Arrange
        await InitializeAsync();
        await PurgeAllQueuesAsync();

        var job1 = await SeedScheduledJobAsync("LogJob1");
        var job2 = await SeedScheduledJobAsync("LogJob2");

        var occurrence1 = await SeedJobOccurrenceAsync(
            jobId: job1.Id,
            jobName: job1.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        var occurrence2 = await SeedJobOccurrenceAsync(
            jobId: job2.Id,
            jobName: job2.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var uniqueCategory = $"MultiTest_{Guid.CreateVersion7():N}";
        var message1 = $"Log for occurrence 1 - {Guid.CreateVersion7():N}";
        var message2 = $"Log for occurrence 2 - {Guid.CreateVersion7():N}";

        // Act - Start the collector first
        var collector = CreateLogCollectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await collector.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(3000, cts.Token);

        // Publish logs for both occurrences
        await PublishLogMessageAsync(new WorkerLogBatchMessage
        {
            BatchTimestamp = DateTime.UtcNow,
            Logs = [
                new WorkerLogMessage
                {
                    CorrelationId = occurrence1.CorrelationId,
                    WorkerId = "worker-1",
                    Log = new OccurrenceLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = "Information",
                        Message = message1,
                        Category = uniqueCategory
                    }
                }
            ]
        }, cts.Token);

        await PublishLogMessageAsync(new WorkerLogBatchMessage
        {
            BatchTimestamp = DateTime.UtcNow,
            Logs = [
                new WorkerLogMessage
                {
                    CorrelationId = occurrence2.CorrelationId,
                    WorkerId = "worker-2",
                    Log = new OccurrenceLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = "Warning",
                        Message = message2,
                        Category = uniqueCategory
                    }
                }
            ]
        }, cts.Token);

        // Wait for both logs
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ1 = await GetOccurrenceAsync(occurrence1.Id, cts.Token);
                var occ2 = await GetOccurrenceAsync(occurrence2.Id, cts.Token);
                return occ1?.Logs?.Any(l => l.Message == message1) == true &&
                       occ2?.Logs?.Any(l => l.Message == message2) == true;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await collector.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("both logs should be processed");

        var updatedOccurrence1 = await GetOccurrenceAsync(occurrence1.Id, cts.Token);
        var updatedOccurrence2 = await GetOccurrenceAsync(occurrence2.Id, cts.Token);

        var log1 = updatedOccurrence1.Logs.FirstOrDefault(l => l.Message == message1);
        var log2 = updatedOccurrence2.Logs.FirstOrDefault(l => l.Message == message2);

        log1.Should().NotBeNull();
        log1!.Level.Should().Be("Information");

        log2.Should().NotBeNull();
        log2!.Level.Should().Be("Warning");
    }

    [Fact]
    public async Task CollectLogs_ShouldHandleDifferentLogLevels()
    {
        // Arrange
        await InitializeAsync();
        await PurgeAllQueuesAsync();

        var job = await SeedScheduledJobAsync("LogLevelJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var uniqueCategory = $"LevelTest_{Guid.CreateVersion7():N}";
        var logLevels = new[] { "Debug", "Information", "Warning", "Error", "Critical" };

        // Act - Start the collector first
        var collector = CreateLogCollectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await collector.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(3000, cts.Token);

        foreach (var level in logLevels)
        {
            await PublishLogMessageAsync(new WorkerLogBatchMessage
            {
                BatchTimestamp = DateTime.UtcNow,
                Logs = [
                    new WorkerLogMessage
                    {
                        CorrelationId = occurrence.CorrelationId,
                        WorkerId = "test-worker",
                        Log = new OccurrenceLog
                        {
                            Timestamp = DateTime.UtcNow,
                            Level = level,
                            Message = $"{level} level log",
                            Category = uniqueCategory
                        }
                    }
                ]
            }, cts.Token);
        }

        // Wait for all 5 logs
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Logs?.Count(l => l.Category == uniqueCategory) >= 5;
            },
            timeout: TimeSpan.FromSeconds(20),
            pollInterval: TimeSpan.FromMilliseconds(100),
            cancellationToken: cts.Token);

        await collector.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("all log levels should be processed");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);

        foreach (var level in logLevels)
        {
            var log = updatedOccurrence.Logs.FirstOrDefault(l => l.Level == level && l.Category == uniqueCategory);
            log.Should().NotBeNull($"{level} log should exist");
            log!.Message.Should().Be($"{level} level log");
        }
    }

    [Fact]
    public async Task CollectLogs_ShouldIncludeLogData()
    {
        // Arrange
        await InitializeAsync();
        await PurgeAllQueuesAsync();

        var job = await SeedScheduledJobAsync("DataLogJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var uniqueCategory = $"DataTest_{Guid.CreateVersion7():N}";
        var logData = new Dictionary<string, object>
        {
            ["recordsProcessed"] = 100,
            ["duration"] = "5.2s",
            ["success"] = true
        };

        // Act - Start the collector first
        var collector = CreateLogCollectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await collector.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(3000, cts.Token);

        await PublishLogMessageAsync(new WorkerLogBatchMessage
        {
            BatchTimestamp = DateTime.UtcNow,
            Logs = [
                new WorkerLogMessage
                {
                    CorrelationId = occurrence.CorrelationId,
                    WorkerId = "test-worker",
                    Log = new OccurrenceLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = "Information",
                        Message = "Log with data",
                        Category = uniqueCategory,
                        Data = logData
                    }
                }
            ]
        }, cts.Token);

        // Wait for log
        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Logs?.Any(l => l.Category == uniqueCategory) == true;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await collector.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("log with data should be processed");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);

        var log = updatedOccurrence.Logs.FirstOrDefault(l => l.Category == uniqueCategory);
        log.Should().NotBeNull();
        log!.Data.Should().NotBeNull();
        log.Data.Should().ContainKey("recordsProcessed");
    }

    [Fact]
    public async Task CollectLogs_ShouldPreserveLogTimestampOrder()
    {
        // Arrange
        await InitializeAsync();
        await PurgeAllQueuesAsync();

        var job = await SeedScheduledJobAsync("TimestampOrderLogJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var uniqueCategory = $"OrderTest_{Guid.CreateVersion7():N}";
        var baseTime = DateTime.UtcNow;

        // Act - Start the collector first
        var collector = CreateLogCollectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await collector.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(3000, cts.Token);

        var logBatch = new WorkerLogBatchMessage
        {
            BatchTimestamp = DateTime.UtcNow,
            Logs = []
        };

        // Send logs with ascending timestamps
        for (int i = 0; i < 3; i++)
        {
            logBatch.Logs.Add(new WorkerLogMessage
            {
                CorrelationId = occurrence.CorrelationId,
                WorkerId = "test-worker",
                Log = new OccurrenceLog
                {
                    Timestamp = baseTime.AddSeconds(i),
                    Level = "Information",
                    Message = $"Ordered log {i}",
                    Category = uniqueCategory
                },
                MessageTimestamp = DateTime.UtcNow
            });
        }

        await PublishLogMessageAsync(logBatch, cts.Token);

        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Logs?.Count(l => l.Category == uniqueCategory) >= 3;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await collector.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("all ordered logs should be processed");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        var orderedLogs = updatedOccurrence.Logs
            .Where(l => l.Category == uniqueCategory)
            .OrderBy(l => l.Timestamp)
            .ToList();

        orderedLogs.Should().HaveCount(3);
        orderedLogs[0].Message.Should().Be("Ordered log 0");
        orderedLogs[1].Message.Should().Be("Ordered log 1");
        orderedLogs[2].Message.Should().Be("Ordered log 2");
        orderedLogs[0].Timestamp.Should().BeBefore(orderedLogs[1].Timestamp);
        orderedLogs[1].Timestamp.Should().BeBefore(orderedLogs[2].Timestamp);
    }

    [Fact]
    public async Task CollectLogs_ShouldHandleLogWithLongMessage()
    {
        // Arrange
        await InitializeAsync();
        await PurgeAllQueuesAsync();

        var job = await SeedScheduledJobAsync("LongMessageLogJob");
        var occurrence = await SeedJobOccurrenceAsync(
            jobId: job.Id,
            jobName: job.JobNameInWorker,
            status: JobOccurrenceStatus.Running
        );

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var uniquePrefix = $"LongMsg_{Guid.CreateVersion7():N}_";
        var longMessage = uniquePrefix + new string('X', 2000);

        // Act
        var collector = CreateLogCollectorService();
        _ = Task.Run(async () =>
        {
            try
            {
                await collector.StartAsync(cts.Token);
                await Task.Delay(Timeout.Infinite, cts.Token);
            }
            catch (OperationCanceledException) { }
        }, cts.Token);

        await Task.Delay(3000, cts.Token);

        await PublishLogMessageAsync(new WorkerLogBatchMessage
        {
            BatchTimestamp = DateTime.UtcNow,
            Logs =
            [
                new()
                {
                    CorrelationId = occurrence.CorrelationId,
                    WorkerId = "test-worker",
                    Log = new OccurrenceLog
                    {
                        Timestamp = DateTime.UtcNow,
                        Level = "Error",
                        Message = longMessage,
                        Category = "LongMsgTest"
                    },
                    MessageTimestamp = DateTime.UtcNow
                }
            ]
        }, cts.Token);

        var found = await WaitForConditionAsync(
            async () =>
            {
                var occ = await GetOccurrenceAsync(occurrence.Id, cts.Token);
                return occ?.Logs?.Any(l => l.Message?.StartsWith(uniquePrefix) == true) == true;
            },
            timeout: TimeSpan.FromSeconds(15),
            pollInterval: TimeSpan.FromMilliseconds(500),
            cancellationToken: cts.Token);

        await collector.StopAsync(cts.Token);

        // Assert
        found.Should().BeTrue("long message log should be processed");

        var updatedOccurrence = await GetOccurrenceAsync(occurrence.Id, cts.Token);
        var log = updatedOccurrence.Logs.First(l => l.Message?.StartsWith(uniquePrefix) == true);
        log.Level.Should().Be("Error");
    }

    private LogCollectorService CreateLogCollectorService() => new(
            _serviceProvider,
            _serviceProvider.GetRequiredService<RabbitMQConnectionFactory>(),
            Options.Create(new LogCollectorOptions
            {
                Enabled = true,
                BatchSize = 1,
                BatchIntervalMs = 100 // Faster batch processing for tests
            }),
            _serviceProvider.GetRequiredService<ILoggerFactory>(),
            _serviceProvider.GetRequiredService<BackgroundServiceMetrics>()
        );

    private async Task PublishLogMessageAsync(WorkerLogBatchMessage message, CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _factory.GetRabbitMqHost(),
            Port = _factory.GetRabbitMqPort(),
            UserName = "guest",
            Password = "guest"
        };

        await using var connection = await factory.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Ensure queue exists
        await channel.QueueDeclareAsync(
            queue: WorkerConstant.Queues.WorkerLogs,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, ConstantJsonOptions.PropNameCaseInsensitive));

        var properties = new BasicProperties
        {
            Persistent = true
        };

        await channel.BasicPublishAsync(
            exchange: "",
            routingKey: WorkerConstant.Queues.WorkerLogs,
            mandatory: false,
            basicProperties: properties,
            body: body,
            cancellationToken: cancellationToken);
    }
}
