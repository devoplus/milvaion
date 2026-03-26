using Dapper;
using Microsoft.Extensions.Options;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using Npgsql;
using ReporterWorker.Models;
using ReporterWorker.Options;
using System.Text.Json;

namespace ReporterWorker.Jobs;

public class WorkerThroughputReportJob(IOptions<ReporterOptions> options) : IAsyncJobWithResult<string>
{
    private readonly ReporterOptions _options = options.Value;

    public async Task<string> ExecuteAsync(IJobContext context)
    {
        context.LogInformation("Starting Worker Throughput Report generation");

        var periodEnd = DateTime.UtcNow;
        var periodStart = periodEnd.AddHours(-_options.ReportGeneration.LookbackHours);

        await using var connection = new NpgsqlConnection(_options.DatabaseConnectionString);
        await connection.OpenAsync(context.CancellationToken);

        var sql = @"
            SELECT 
                ""WorkerId"",
                COUNT(*) as job_count,
                SUM(CASE WHEN ""Status"" = 2 THEN 1 ELSE 0 END) as success_count,
                SUM(CASE WHEN ""Status"" = 3 THEN 1 ELSE 0 END) as failure_count,
                AVG(""DurationMs"") as avg_duration
            FROM ""JobOccurrences""
            WHERE ""StartTime"" >= @PeriodStart
                AND ""StartTime"" < @PeriodEnd
                AND ""WorkerId"" IS NOT NULL
            GROUP BY ""WorkerId""
            ORDER BY job_count DESC";

        var workerStats = await connection.QueryAsync<(string WorkerId, int JobCount, int SuccessCount, int FailureCount, double AvgDuration)>(
            sql,
            new { PeriodStart = periodStart, PeriodEnd = periodEnd });

        var data = new WorkerThroughputData
        {
            Workers = [.. workerStats.Select(s => new WorkerThroughputInfo
            {
                WorkerId = s.WorkerId,
                JobCount = s.JobCount,
                SuccessCount = s.SuccessCount,
                FailureCount = s.FailureCount,
                AverageDurationMs = s.AvgDuration
            })]
        };

        var reportId = Guid.CreateVersion7();
        var report = new MetricReport
        {
            Id = reportId,
            MetricType = MetricTypes.WorkerThroughput,
            DisplayName = "Worker Throughput",
            Description = "Job count processed by each worker over time",
            Data = JsonSerializer.Serialize(data),
            PeriodStartTime = periodStart,
            PeriodEndTime = periodEnd,
            GeneratedAt = DateTime.UtcNow,
            Tags = "worker,throughput,performance"
        };

        var insertSql = @"
            INSERT INTO ""MetricReports""
            (""Id"", ""MetricType"", ""DisplayName"", ""Description"", ""Data"",
             ""PeriodStartTime"", ""PeriodEndTime"", ""GeneratedAt"", ""Tags"", ""CreationDate"")
            VALUES
            (@Id, @MetricType, @DisplayName, @Description, @Data::jsonb,
             @PeriodStartTime, @PeriodEndTime, @GeneratedAt, @Tags, @CreationDate)";

        await connection.ExecuteAsync(insertSql, new
        {
            report.Id,
            report.MetricType,
            report.DisplayName,
            report.Description,
            report.Data,
            report.PeriodStartTime,
            report.PeriodEndTime,
            report.GeneratedAt,
            report.Tags,
            CreationDate = DateTime.UtcNow
        });

        context.LogInformation($"Worker Throughput Report generated for {data.Workers.Count} workers");

        return JsonSerializer.Serialize(new { Success = true, ReportId = reportId, WorkerCount = data.Workers.Count });
    }
}
