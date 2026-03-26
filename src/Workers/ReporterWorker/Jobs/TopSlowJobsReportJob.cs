using Dapper;
using Microsoft.Extensions.Options;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using Npgsql;
using ReporterWorker.Models;
using ReporterWorker.Options;
using System.Text.Json;

namespace ReporterWorker.Jobs;

public class TopSlowJobsReportJob(IOptions<ReporterOptions> options) : IAsyncJobWithResult<string>
{
    private readonly ReporterOptions _options = options.Value;

    public async Task<string> ExecuteAsync(IJobContext context)
    {
        context.LogInformation("Starting Top Slow Jobs Report generation");

        var periodEnd = DateTime.UtcNow;
        var periodStart = periodEnd.AddHours(-_options.ReportGeneration.LookbackHours);

        await using var connection = new NpgsqlConnection(_options.DatabaseConnectionString);
        await connection.OpenAsync(context.CancellationToken);

        var sql = @"
            SELECT 
                ""JobName"",
                AVG(""DurationMs"") as avg_duration,
                COUNT(*) as occurrence_count
            FROM ""JobOccurrences""
            WHERE ""StartTime"" >= @PeriodStart
                AND ""StartTime"" < @PeriodEnd
                AND ""DurationMs"" IS NOT NULL
            GROUP BY ""JobName""
            ORDER BY avg_duration DESC
            LIMIT @TopN";

        var jobStats = await connection.QueryAsync<(string JobName, double AvgDuration, int OccurrenceCount)>(
            sql,
            new { PeriodStart = periodStart, PeriodEnd = periodEnd, TopN = _options.ReportGeneration.TopNLimit });

        var data = new TopSlowJobsData
        {
            Jobs = [.. jobStats.Select(s => new JobDurationInfo
            {
                JobName = s.JobName,
                AverageDurationMs = s.AvgDuration,
                OccurrenceCount = s.OccurrenceCount
            })]
        };

        var reportId = Guid.CreateVersion7();
        var report = new MetricReport
        {
            Id = reportId,
            MetricType = MetricTypes.TopSlowJobs,
            DisplayName = "Top Slow Jobs",
            Description = "Average duration by job name",
            Data = JsonSerializer.Serialize(data),
            PeriodStartTime = periodStart,
            PeriodEndTime = periodEnd,
            GeneratedAt = DateTime.UtcNow,
            Tags = "performance,slow,top"
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

        context.LogInformation($"Top Slow Jobs Report generated with {data.Jobs.Count} jobs");

        return JsonSerializer.Serialize(new { Success = true, ReportId = reportId, JobCount = data.Jobs.Count });
    }
}
