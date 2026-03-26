using Dapper;
using Microsoft.Extensions.Options;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using Npgsql;
using ReporterWorker.Models;
using ReporterWorker.Options;
using System.Text.Json;

namespace ReporterWorker.Jobs;

public class CronScheduleVsActualReportJob(IOptions<ReporterOptions> options) : IAsyncJobWithResult<string>
{
    private readonly ReporterOptions _options = options.Value;

    public async Task<string> ExecuteAsync(IJobContext context)
    {
        context.LogInformation("Starting Cron Schedule vs Actual Report generation");

        var periodEnd = DateTime.UtcNow;
        var periodStart = periodEnd.AddHours(-_options.ReportGeneration.LookbackHours);

        await using var connection = new NpgsqlConnection(_options.DatabaseConnectionString);
        await connection.OpenAsync(context.CancellationToken);

        var sql = @"
            SELECT 
                jo.""Id"" as occurrence_id,
                jo.""JobId"" as job_id,
                jo.""JobName"",
                jo.""CreatedAt"" as scheduled_time,
                jo.""StartTime"" as actual_time,
                EXTRACT(EPOCH FROM (jo.""StartTime"" - jo.""CreatedAt"")) as deviation_seconds
            FROM ""JobOccurrences"" jo
            INNER JOIN ""ScheduledJobs"" sj ON jo.""JobId"" = sj.""Id""
            WHERE jo.""StartTime"" >= @PeriodStart
                AND jo.""StartTime"" < @PeriodEnd
                AND sj.""CronExpression"" IS NOT NULL
                AND jo.""Status"" IN (2, 3)
            ORDER BY ABS(EXTRACT(EPOCH FROM (jo.""StartTime"" - jo.""CreatedAt""))) DESC
            LIMIT @TopN";

        var deviations = await connection.QueryAsync<(Guid OccurrenceId, Guid JobId, string JobName, DateTime ScheduledTime, DateTime ActualTime, double DeviationSeconds)>(
            sql,
            new { PeriodStart = periodStart, PeriodEnd = periodEnd, TopN = _options.ReportGeneration.MaxScheduleDeviations });

        var data = new CronScheduleVsActualData
        {
            Jobs = [.. deviations.Select(d => new ScheduleDeviationInfo
            {
                OccurrenceId = d.OccurrenceId,
                JobId = d.JobId,
                JobName = d.JobName,
                ScheduledTime = d.ScheduledTime,
                ActualTime = d.ActualTime,
                DeviationSeconds = d.DeviationSeconds
            })]
        };

        var reportId = Guid.CreateVersion7();
        var report = new MetricReport
        {
            Id = reportId,
            MetricType = MetricTypes.CronScheduleVsActual,
            DisplayName = "Cron Schedule vs Actual",
            Description = "Scheduled vs actual execution time deviation",
            Data = JsonSerializer.Serialize(data),
            PeriodStartTime = periodStart,
            PeriodEndTime = periodEnd,
            GeneratedAt = DateTime.UtcNow,
            Tags = "scheduling,cron,deviation,timing"
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

        context.LogInformation($"Cron Schedule vs Actual Report generated with {data.Jobs.Count} deviations");

        return JsonSerializer.Serialize(new { Success = true, ReportId = reportId, DeviationCount = data.Jobs.Count });
    }
}
