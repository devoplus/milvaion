using Dapper;
using Microsoft.Extensions.Options;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using Npgsql;
using ReporterWorker.Models;
using ReporterWorker.Options;
using System.Text.Json;

namespace ReporterWorker.Jobs;

public class WorkflowDurationTrendReportJob(IOptions<ReporterOptions> options) : IAsyncJobWithResult<string>
{
    private readonly ReporterOptions _options = options.Value;

    public async Task<string> ExecuteAsync(IJobContext context)
    {
        context.LogInformation("Starting Workflow Duration Trend Report generation");

        var periodEnd = DateTime.UtcNow;
        var periodStart = periodEnd.AddHours(-_options.ReportGeneration.LookbackHours);

        await using var connection = new NpgsqlConnection(_options.DatabaseConnectionString);
        await connection.OpenAsync(context.CancellationToken);

        var sql = @"
            SELECT 
                DATE_TRUNC('hour', wr.""StartTime"") as hour,
                w.""Name"" as workflow_name,
                AVG(wr.""DurationMs"") as avg_duration_ms
            FROM ""WorkflowRuns"" wr
            INNER JOIN ""Workflows"" w ON wr.""WorkflowId"" = w.""Id""
            WHERE wr.""StartTime"" >= @PeriodStart
                AND wr.""StartTime"" < @PeriodEnd
                AND wr.""Status"" IN (2, 3, 5)
                AND wr.""DurationMs"" IS NOT NULL
            GROUP BY DATE_TRUNC('hour', wr.""StartTime""), w.""Name""
            ORDER BY hour";

        var rows = await connection.QueryAsync<(DateTime Hour, string WorkflowName, double AvgDurationMs)>(
            sql,
            new { PeriodStart = periodStart, PeriodEnd = periodEnd });

        var data = new WorkflowDurationTrendData
        {
            DataPoints = [.. rows.GroupBy(r => r.Hour)
                .OrderBy(g => g.Key)
                .Select(g => new WorkflowDurationPoint
                {
                    Timestamp = g.Key,
                    WorkflowAvgDurationMs = g.ToDictionary(r => r.WorkflowName, r => Math.Round(r.AvgDurationMs, 2))
                })]
        };

        var reportId = Guid.CreateVersion7();
        var report = new MetricReport
        {
            Id = reportId,
            MetricType = MetricTypes.WorkflowDurationTrend,
            DisplayName = "Workflow Duration Trend",
            Description = "Workflow execution duration over time",
            Data = JsonSerializer.Serialize(data),
            PeriodStartTime = periodStart,
            PeriodEndTime = periodEnd,
            GeneratedAt = DateTime.UtcNow,
            Tags = "workflow,duration,trend,timeseries"
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

        context.LogInformation($"Workflow Duration Trend Report generated with {data.DataPoints.Count} time points");

        return JsonSerializer.Serialize(new { Success = true, ReportId = reportId, TimePoints = data.DataPoints.Count });
    }
}
