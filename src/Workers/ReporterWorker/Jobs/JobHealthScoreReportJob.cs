using Dapper;
using Microsoft.Extensions.Options;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using Npgsql;
using ReporterWorker.Models;
using ReporterWorker.Options;
using System.Text.Json;

namespace ReporterWorker.Jobs;

public class JobHealthScoreReportJob(IOptions<ReporterOptions> options) : IAsyncJobWithResult<string>
{
    private readonly ReporterOptions _options = options.Value;

    public async Task<string> ExecuteAsync(IJobContext context)
    {
        context.LogInformation("Starting Job Health Score Report generation");

        var periodEnd = DateTime.UtcNow;
        var periodStart = periodEnd.AddHours(-_options.ReportGeneration.LookbackHours);

        await using var connection = new NpgsqlConnection(_options.DatabaseConnectionString);
        await connection.OpenAsync(context.CancellationToken);

        var sql = @"
            SELECT 
                ""JobName"",
                COUNT(*) as total_occurrences,
                SUM(CASE WHEN ""Status"" = 2 THEN 1 ELSE 0 END) as success_count,
                SUM(CASE WHEN ""Status"" = 3 THEN 1 ELSE 0 END) as failure_count
            FROM ""JobOccurrences""
            WHERE ""StartTime"" >= @PeriodStart
                AND ""StartTime"" < @PeriodEnd
            GROUP BY ""JobName""
            HAVING COUNT(*) >= 5
            ORDER BY (CAST(SUM(CASE WHEN ""Status"" = 2 THEN 1 ELSE 0 END) AS FLOAT) / COUNT(*)) ASC";

        var jobStats = await connection.QueryAsync<(string JobName, int TotalOccurrences, int SuccessCount, int FailureCount)>(
            sql,
            new { PeriodStart = periodStart, PeriodEnd = periodEnd });

        var data = new JobHealthScoreData
        {
            Jobs = [.. jobStats.Select(s => new JobHealthInfo
            {
                JobName = s.JobName,
                TotalOccurrences = s.TotalOccurrences,
                SuccessCount = s.SuccessCount,
                FailureCount = s.FailureCount,
                SuccessRate = s.TotalOccurrences > 0 ? (s.SuccessCount * 100.0 / s.TotalOccurrences) : 0
            })]
        };

        var reportId = Guid.CreateVersion7();
        var report = new MetricReport
        {
            Id = reportId,
            MetricType = MetricTypes.JobHealthScore,
            DisplayName = "Job Health Score",
            Description = "Success rate for last N executions per job",
            Data = JsonSerializer.Serialize(data),
            PeriodStartTime = periodStart,
            PeriodEndTime = periodEnd,
            GeneratedAt = DateTime.UtcNow,
            Tags = "health,reliability,success-rate"
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

        context.LogInformation($"Job Health Score Report generated for {data.Jobs.Count} jobs");

        return JsonSerializer.Serialize(new { Success = true, ReportId = reportId, JobCount = data.Jobs.Count });
    }
}
