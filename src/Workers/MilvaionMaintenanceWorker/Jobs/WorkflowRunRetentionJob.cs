using Dapper;
using Microsoft.Extensions.Options;
using MilvaionMaintenanceWorker.Options;
using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using Npgsql;
using System.Text.Json;

namespace MilvaionMaintenanceWorker.Jobs;

/// <summary>
/// Cleans up old workflow runs based on retention policy.
/// Prevents database bloat from accumulating historical workflow data.
/// Recommended schedule: Daily at 2:30 AM.
/// </summary>
public class WorkflowRunRetentionJob(IOptions<MaintenanceOptions> options) : IAsyncJobWithResult<string>
{
    private readonly MaintenanceOptions _options = options.Value;

    public async Task<string> ExecuteAsync(IJobContext context)
    {
        var settings = _options.WorkflowRunRetention;
        var results = new Dictionary<string, int>();
        var totalDeleted = 0;

        context.LogInformation("[WORKFLOW-RETENTION] Workflow run retention cleanup started");
        context.LogInformation($"Retention: Completed={settings.CompletedRetentionDays}d, Failed={settings.FailedRetentionDays}d, Cancelled={settings.CancelledRetentionDays}d, PartiallyCompleted={settings.PartiallyCompletedRetentionDays}d");

        await using var connection = new NpgsqlConnection(_options.DatabaseConnectionString);

        await connection.OpenAsync(context.CancellationToken);

        // 1. Delete old COMPLETED workflow runs
        var completedDeleted = await DeleteWorkflowRunsByStatusAsync(connection, 2, settings.CompletedRetentionDays, settings.BatchSize, context);

        results["Completed"] = completedDeleted;
        totalDeleted += completedDeleted;

        // 2. Delete old FAILED workflow runs
        var failedDeleted = await DeleteWorkflowRunsByStatusAsync(connection, 3, settings.FailedRetentionDays, settings.BatchSize, context);

        results["Failed"] = failedDeleted;
        totalDeleted += failedDeleted;

        // 3. Delete old CANCELLED workflow runs
        var cancelledDeleted = await DeleteWorkflowRunsByStatusAsync(connection, 4, settings.CancelledRetentionDays, settings.BatchSize, context);

        results["Cancelled"] = cancelledDeleted;
        totalDeleted += cancelledDeleted;

        // 4. Delete old PARTIALLY COMPLETED workflow runs
        var partiallyCompletedDeleted = await DeleteWorkflowRunsByStatusAsync(connection, 5, settings.PartiallyCompletedRetentionDays, settings.BatchSize, context);

        results["PartiallyCompleted"] = partiallyCompletedDeleted;
        totalDeleted += partiallyCompletedDeleted;

        context.LogInformation($"[DONE] Workflow run retention cleanup completed. Total deleted: {totalDeleted} workflow runs");

        // OPTIONAL: Run VACUUM if enabled and threshold met
        if (settings.VacuumAfterCleanup && totalDeleted >= settings.VacuumThreshold)
        {
            context.LogInformation($"[VACUUM] Running VACUUM ANALYZE to reclaim space (deleted {totalDeleted} workflow runs)...");

            try
            {
                await connection.ExecuteAsync("VACUUM ANALYZE \"WorkflowRuns\"");
                context.LogInformation("  [OK] VACUUM WorkflowRuns completed");
            }
            catch (Exception ex)
            {
                context.LogWarning($"  [WARNING] VACUUM failed: {ex.Message} (non-critical, autovacuum will handle)");
            }
        }
        else
        {
            context.LogInformation($"[SKIP] VACUUM skipped (VacuumAfterCleanup={settings.VacuumAfterCleanup}, deleted={totalDeleted}, threshold={settings.VacuumThreshold})");
        }

        return JsonSerializer.Serialize(new
        {
            Success = true,
            TotalDeleted = totalDeleted,
            Details = results
        });
    }

    private static async Task<int> DeleteWorkflowRunsByStatusAsync(NpgsqlConnection connection,
                                                                   int status,
                                                                   int retentionDays,
                                                                   int batchSize,
                                                                   IJobContext context)
    {
        var statusName = status switch
        {
            2 => "Completed",
            3 => "Failed",
            4 => "Cancelled",
            5 => "PartiallyCompleted",
            _ => $"Status{status}"
        };

        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var totalDeleted = 0;
        int deletedInBatch;

        context.LogInformation($"  Deleting {statusName} workflow runs older than {cutoffDate:yyyy-MM-dd}...");

        // Delete in batches to avoid long locks
        // Note: Associated JobOccurrences will be cascade deleted via FK relationship
        do
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var sql = @"
                DELETE FROM ""WorkflowRuns""
                WHERE ""Id"" IN (
                    SELECT ""Id"" FROM ""WorkflowRuns""
                    WHERE ""Status"" = @Status
                    AND (
                        (""EndTime"" IS NOT NULL AND ""EndTime"" < @CutoffDate)
                        OR (""EndTime"" IS NULL AND ""CreatedAt"" < @CutoffDate)
                    )
                    LIMIT @BatchSize
                )";

            deletedInBatch = await connection.ExecuteAsync(sql, new
            {
                Status = status,
                CutoffDate = cutoffDate,
                BatchSize = batchSize
            });

            totalDeleted += deletedInBatch;

            if (deletedInBatch > 0)
                context.LogInformation($"    Deleted batch: {deletedInBatch} workflow runs (total: {totalDeleted})");

        } while (deletedInBatch == batchSize);

        context.LogInformation($"  [OK] {statusName}: Deleted {totalDeleted} workflow runs");

        return totalDeleted;
    }
}
