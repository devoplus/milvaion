using Dapper;
using Microsoft.Extensions.Options;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using Npgsql;
using ReporterWorker.Models;
using ReporterWorker.Options;
using System.Text.Json;

namespace ReporterWorker.Jobs;

public class FailureRateTrendReportJob(IOptions<ReporterOptions> options) : IAsyncJobWithResult<string>
{
    private readonly ReporterOptions _options = options.Value;

    public async Task<string> ExecuteAsync(IJobContext context)
    {
        context.LogInformation("Starting Failure Rate Trend Report generation");

        var periodEnd = DateTime.UtcNow;
        var periodStart = periodEnd.AddHours(-_options.ReportGeneration.LookbackHours);

        await using var connection = new NpgsqlConnection(_options.DatabaseConnectionString);
        await connection.OpenAsync(context.CancellationToken);

        var sql = @"
            SELECT 
                DATE_TRUNC('hour', ""StartTime"") as hour,
                COUNT(*) as total,
                SUM(CASE WHEN ""Status"" = 3 THEN 1 ELSE 0 END) as failed
            FROM ""JobOccurrences""
            WHERE ""StartTime"" >= @PeriodStart AND ""StartTime"" < @PeriodEnd
            GROUP BY DATE_TRUNC('hour', ""StartTime"")
            ORDER BY hour";

        var hourlyStats = await connection.QueryAsync<(DateTime Hour, int Total, int Failed)>(
            sql,
            new { PeriodStart = periodStart, PeriodEnd = periodEnd });

        var data = new FailureRateTrendData
        {
            ThresholdPercentage = 5.0,
            DataPoints = [.. hourlyStats.Select(s => new TimeSeriesPoint
            {
                Timestamp = s.Hour,
                Value = s.Total > 0 ? (s.Failed * 100.0 / s.Total) : 0
            })]
        };

        var reportId = Guid.CreateVersion7();
        var report = new MetricReport
        {
            Id = reportId,
            MetricType = MetricTypes.FailureRateTrend,
            DisplayName = "Failure Rate Trend",
            Description = "Error rate changes over time",
            Data = JsonSerializer.Serialize(data),
            PeriodStartTime = periodStart,
            PeriodEndTime = periodEnd,
            GeneratedAt = DateTime.UtcNow,
            Tags = "trend,failure,monitoring"
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

        context.LogInformation($"Failure Rate Trend Report generated with {data.DataPoints.Count} data points");

        return JsonSerializer.Serialize(new { Success = true, ReportId = reportId, DataPoints = data.DataPoints.Count });
    }
}
