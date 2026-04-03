using Dapper;
using Microsoft.Extensions.Options;
using Milvasoft.Milvaion.Sdk.Domain;
using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using Npgsql;
using ReporterWorker.Models;
using ReporterWorker.Options;
using System.Text.Json;

namespace ReporterWorker.Jobs;

public class WorkerUtilizationTrendReportJob(IOptions<ReporterOptions> options) : IAsyncJobWithResult<string>
{
    private readonly ReporterOptions _options = options.Value;

    public async Task<string> ExecuteAsync(IJobContext context)
    {
        context.LogInformation("Starting Worker Utilization Trend Report generation");

        var periodEnd = DateTime.UtcNow;
        var periodStart = periodEnd.AddHours(-_options.ReportGeneration.LookbackHours);

        await using var connection = new NpgsqlConnection(_options.DatabaseConnectionString);
        await connection.OpenAsync(context.CancellationToken);

        var sql = @"
            SELECT 
                DATE_TRUNC('hour', ""StartTime"") as hour,
                ""WorkerId"",
                COUNT(*) as job_count,
                CAST(SUM(""DurationMs"") AS bigint) as total_duration_ms
            FROM ""JobOccurrences""
            WHERE ""StartTime"" >= @PeriodStart
                AND ""StartTime"" < @PeriodEnd
                AND ""WorkerId"" IS NOT NULL
                AND ""DurationMs"" IS NOT NULL
            GROUP BY DATE_TRUNC('hour', ""StartTime""), ""WorkerId""
            ORDER BY hour, ""WorkerId""";

        var queryTimeout = _options.ReportGeneration.QueryTimeoutSeconds;

        var hourlyStats = await connection.QueryAsync<(DateTime Hour, string WorkerId, int JobCount, long TotalDurationMs)>(
            new CommandDefinition(sql, new { PeriodStart = periodStart, PeriodEnd = periodEnd },
                commandTimeout: queryTimeout, cancellationToken: context.CancellationToken));

        var groupedByHour = hourlyStats
            .GroupBy(s => s.Hour)
            .OrderBy(g => g.Key)
            .ToList();

        var data = new WorkerUtilizationTrendData
        {
            DataPoints = [.. groupedByHour.Select(hourGroup => new UtilizationPoint
            {
                Timestamp = hourGroup.Key,
                WorkerUtilization = hourGroup.ToDictionary(
                    w => w.WorkerId,
                    w => CalculateUtilization(w.TotalDurationMs)
                )
            })]
        };

        var reportId = Guid.CreateVersion7();
        var report = new MetricReport
        {
            Id = reportId,
            MetricType = MetricTypes.WorkerUtilizationTrend,
            DisplayName = "Worker Utilization Trend",
            Description = "Capacity vs actual utilization rate (time series)",
            Data = JsonSerializer.Serialize(data),
            PeriodStartTime = periodStart,
            PeriodEndTime = periodEnd,
            GeneratedAt = DateTime.UtcNow,
            Tags = "worker,utilization,capacity,trend"
        };

        var insertSql = @"
            INSERT INTO ""MetricReports""
            (""Id"", ""MetricType"", ""DisplayName"", ""Description"", ""Data"",
             ""PeriodStartTime"", ""PeriodEndTime"", ""GeneratedAt"", ""Tags"", ""CreationDate"")
            VALUES
            (@Id, @MetricType, @DisplayName, @Description, @Data::jsonb,
             @PeriodStartTime, @PeriodEndTime, @GeneratedAt, @Tags, @CreationDate)";

        await connection.ExecuteAsync(new CommandDefinition(insertSql, new
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
        }, commandTimeout: queryTimeout, cancellationToken: context.CancellationToken));

        context.LogInformation($"Worker Utilization Trend Report generated with {data.DataPoints.Count} time points");

        return JsonSerializer.Serialize(new { Success = true, ReportId = reportId, TimePoints = data.DataPoints.Count });
    }

    private static double CalculateUtilization(long totalDurationMs)
    {
        var hourInMs = 3600000;
        var utilization = (totalDurationMs * 100.0) / hourInMs;
        return Math.Min(utilization, 100);
    }
}
