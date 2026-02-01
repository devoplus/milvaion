using Dapper;
using Microsoft.Extensions.Options;
using MilvaionMaintenanceWorker.Options;
using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using Npgsql;
using System.Text.Json;

namespace MilvaionMaintenanceWorker.Jobs;

/// <summary>
/// Archives old job occurrences to a dated archive table instead of deleting them.
/// Creates a new table for each archive run (e.g., JobOccurrences_Archive_2024_01).
/// Useful for compliance, auditing, or historical analysis.
/// Recommended schedule: Monthly (1st day of month, 04:00).
/// </summary>
public class OccurrenceArchiveJob(IOptions<MaintenanceOptions> options) : IAsyncJobWithResult
{
    private readonly MaintenanceOptions _options = options.Value;

    public async Task<string> ExecuteAsync(IJobContext context)
    {
        var settings = _options.OccurrenceArchive;

        context.LogInformation("[ARCHIVE] Occurrence archive job started");
        context.LogInformation($"Archive occurrences older than {settings.ArchiveAfterDays} days");
        context.LogInformation($"Statuses to archive: {string.Join(", ", settings.StatusesToArchive)}");

        await using var connection = new NpgsqlConnection(_options.DatabaseConnectionString);
        await connection.OpenAsync(context.CancellationToken);

        var cutoffDate = DateTime.UtcNow.AddDays(-settings.ArchiveAfterDays);
        var archiveTableName = GenerateArchiveTableName(settings.ArchiveTablePrefix);

        context.LogInformation($"Cutoff date: {cutoffDate:yyyy-MM-dd HH:mm:ss}");
        context.LogInformation($"Archive table: {archiveTableName}");

        // 1. First check if there are any records to archive
        var statusFilter = string.Join(", ", settings.StatusesToArchive);
        var countToArchive = await connection.ExecuteScalarAsync<int>($@"
            SELECT COUNT(*) FROM ""JobOccurrences""
            WHERE ""Status"" IN ({statusFilter})
            AND (
                (""EndTime"" IS NOT NULL AND ""EndTime"" < @CutoffDate)
                OR (""EndTime"" IS NULL AND ""CreatedAt"" < @CutoffDate)
            )", new { CutoffDate = cutoffDate });

        context.LogInformation($"Found {countToArchive} occurrences to archive");

        // 2. If nothing to archive, return early without creating table
        if (countToArchive == 0)
        {
            context.LogInformation("[DONE] No occurrences to archive");
            return JsonSerializer.Serialize(new
            {
                Success = true,
                ArchivedCount = 0,
                ArchiveTable = (string)null,
                Message = "No occurrences matched the archive criteria"
            });
        }

        // 3. Create archive table only if we have records to archive
        await CreateArchiveTableIfNotExistsAsync(connection, archiveTableName, context);

        // 3.1. Also create archive table for logs
        var archiveLogsTableName = $"{archiveTableName}_Logs";
        await CreateArchiveLogsTableIfNotExistsAsync(connection, archiveLogsTableName, context);

        // 4. Archive in batches
        var totalArchived = 0;
        var totalLogsArchived = 0;
        int archivedInBatch;

        do
        {
            context.CancellationToken.ThrowIfCancellationRequested();

            var (archived, logsArchived) = await ArchiveBatchAsync(connection, archiveTableName, archiveLogsTableName, settings.StatusesToArchive, cutoffDate, settings.BatchSize);

            totalArchived += archived;
            totalLogsArchived += logsArchived;
            archivedInBatch = archived;

            if (archivedInBatch > 0)
            {
                context.LogInformation($"  Archived batch: {archivedInBatch} occurrences, {logsArchived} logs (total: {totalArchived} occurrences, {totalLogsArchived} logs)");
            }
        } while (archivedInBatch == settings.BatchSize);

        // 4. Get archive table size
        var archiveTableSize = await GetTableSizeAsync(connection, archiveTableName);
        var archiveLogsTableSize = await GetTableSizeAsync(connection, archiveLogsTableName);

        context.LogInformation($"[DONE] Archive completed. Total archived: {totalArchived} occurrences, {totalLogsArchived} logs");
        context.LogInformation($"Archive table size: {FormatBytes(archiveTableSize)}");
        context.LogInformation($"Archive logs table size: {FormatBytes(archiveLogsTableSize)}");

        // 5. Optionally create index on archive table
        if (settings.CreateIndexOnArchive && totalArchived > 0)
        {
            await CreateArchiveIndexesAsync(connection, archiveTableName, archiveLogsTableName, context);
        }

        // 6. OPTIONAL: Run VACUUM if enabled and threshold met
        if (settings.VacuumAfterArchive && (totalArchived >= settings.VacuumThreshold || totalLogsArchived >= (settings.VacuumThreshold * 5)))
        {
            context.LogInformation($"[VACUUM] Running VACUUM ANALYZE to reclaim space (archived {totalArchived} occurrences, {totalLogsArchived} logs)...");

            try
            {
                // VACUUM ANALYZE both tables to reclaim space after archive
                await connection.ExecuteAsync("VACUUM ANALYZE \"JobOccurrences\"");
                context.LogInformation("  [OK] VACUUM JobOccurrences completed");

                await connection.ExecuteAsync("VACUUM ANALYZE \"JobOccurrenceLogs\"");
                context.LogInformation("  [OK] VACUUM JobOccurrenceLogs completed");
            }
            catch (Exception ex)
            {
                context.LogWarning($"  [WARNING] VACUUM failed: {ex.Message} (non-critical, autovacuum will handle)");
            }
        }
        else
        {
            context.LogInformation($"[SKIP] VACUUM skipped (VacuumAfterArchive={settings.VacuumAfterArchive}, archived={totalArchived}, threshold={settings.VacuumThreshold})");
        }

        return JsonSerializer.Serialize(new
        {
            Success = true,
            ArchivedCount = totalArchived,
            ArchivedLogsCount = totalLogsArchived,
            ArchiveTable = archiveTableName,
            ArchiveLogsTable = archiveLogsTableName,
            ArchiveTableSize = archiveTableSize,
            ArchiveLogsTableSize = archiveLogsTableSize,
            CutoffDate = cutoffDate
        });
    }

    private static string GenerateArchiveTableName(string prefix)
    {
        var now = DateTime.UtcNow;
        return $"{prefix}_{now:yyyy}_{now:MM}";
    }

    private static async Task CreateArchiveTableIfNotExistsAsync(
        NpgsqlConnection connection,
        string archiveTableName,
        IJobContext context)
    {
        // Check if table exists
        var tableExists = await connection.ExecuteScalarAsync<bool>(@"
            SELECT EXISTS (
                SELECT FROM information_schema.tables 
                WHERE table_name = @TableName
            )", new { TableName = archiveTableName });

        if (tableExists)
        {
            context.LogInformation($"  Archive table {archiveTableName} already exists");
            return;
        }

        context.LogInformation($"  Creating archive table {archiveTableName}...");

        // Create table with same structure as JobOccurrences
        var createTableSql = $@"
            CREATE TABLE ""{archiveTableName}"" (
                ""Id"" uuid NOT NULL,
                ""JobId"" uuid NOT NULL,
                ""CorrelationId"" uuid NOT NULL,
                ""Status"" integer NOT NULL,
                ""WorkerId"" varchar(200),
                ""JobName"" varchar(200),
                ""ScheduledTime"" timestamp with time zone NOT NULL,
                ""StartTime"" timestamp with time zone,
                ""EndTime"" timestamp with time zone,
                ""DurationMs"" bigint,
                ""Result"" text,
                ""Exception"" text,
                ""StatusChangeLogs"" jsonb,
                ""LastHeartbeat"" timestamp with time zone,
                ""DispatchRetryCount"" integer NOT NULL DEFAULT 0,
                ""NextDispatchRetryAt"" timestamp with time zone,
                ""CreatedAt"" timestamp with time zone NOT NULL,
                ""ArchivedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT ""PK_{archiveTableName}"" PRIMARY KEY (""Id"")
            )";

        await connection.ExecuteAsync(createTableSql);
        context.LogInformation($"  [OK] Archive table created");
    }

    private static async Task CreateArchiveLogsTableIfNotExistsAsync(
        NpgsqlConnection connection,
        string archiveLogsTableName,
        IJobContext context)
    {
        // Check if table exists
        var tableExists = await connection.ExecuteScalarAsync<bool>(@"
            SELECT EXISTS (
                SELECT FROM information_schema.tables 
                WHERE table_name = @TableName
            )", new { TableName = archiveLogsTableName });

        if (tableExists)
        {
            context.LogInformation($"  Archive logs table {archiveLogsTableName} already exists");
            return;
        }

        context.LogInformation($"  Creating archive logs table {archiveLogsTableName}...");

        // Create table with same structure as JobOccurrenceLogs
        var createTableSql = $@"
            CREATE TABLE ""{archiveLogsTableName}"" (
                ""Id"" uuid NOT NULL,
                ""OccurrenceId"" uuid NOT NULL,
                ""Level"" integer NOT NULL,
                ""Message"" text NOT NULL,
                ""Data"" jsonb,
                ""Timestamp"" timestamp with time zone NOT NULL,
                ""ArchivedAt"" timestamp with time zone NOT NULL DEFAULT NOW(),
                CONSTRAINT ""PK_{archiveLogsTableName}"" PRIMARY KEY (""Id"")
            )";

        await connection.ExecuteAsync(createTableSql);
        context.LogInformation($"  [OK] Archive logs table created");
    }

    private static async Task<(int archivedOccurrences, int archivedLogs)> ArchiveBatchAsync(
        NpgsqlConnection connection,
        string archiveTableName,
        string archiveLogsTableName,
        List<int> statuses,
        DateTime cutoffDate,
        int batchSize)
    {
        var statusFilter = string.Join(", ", statuses);

        // Archive occurrences and their logs atomically
        var archiveSql = $@"
            WITH to_archive AS (
                SELECT ""Id"" FROM ""JobOccurrences""
                WHERE ""Status"" IN ({statusFilter})
                AND (
                    (""EndTime"" IS NOT NULL AND ""EndTime"" < @CutoffDate)
                    OR (""EndTime"" IS NULL AND ""CreatedAt"" < @CutoffDate)
                )
                LIMIT @BatchSize
            ),
            archived_logs AS (
                INSERT INTO ""{archiveLogsTableName}"" (
                    ""Id"", ""OccurrenceId"", ""Level"", ""Message"", ""Data"", ""Timestamp"", ""ArchivedAt""
                )
                SELECT 
                    jol.""Id"", jol.""OccurrenceId"", jol.""Level"", jol.""Message"", jol.""Data"", jol.""Timestamp"", NOW()
                FROM ""JobOccurrenceLogs"" jol
                INNER JOIN to_archive ta ON jol.""OccurrenceId"" = ta.""Id""
                RETURNING ""Id""
            ),
            deleted_logs AS (
                DELETE FROM ""JobOccurrenceLogs""
                WHERE ""OccurrenceId"" IN (SELECT ""Id"" FROM to_archive)
                RETURNING 1
            ),
            inserted AS (
                INSERT INTO ""{archiveTableName}"" (
                    ""Id"", ""JobId"", ""CorrelationId"", ""Status"", ""WorkerId"", ""JobName"",
                    ""ScheduledTime"", ""StartTime"", ""EndTime"", ""DurationMs"",
                    ""Result"", ""Exception"", ""StatusChangeLogs"",
                    ""LastHeartbeat"", ""DispatchRetryCount"", ""NextDispatchRetryAt"", ""CreatedAt"", ""ArchivedAt""
                )
                SELECT
                    jo.""Id"", jo.""JobId"", jo.""CorrelationId"", jo.""Status"", jo.""WorkerId"", jo.""JobName"",
                    jo.""ScheduledTime"", jo.""StartTime"", jo.""EndTime"", jo.""DurationMs"",
                    jo.""Result"", jo.""Exception"", jo.""StatusChangeLogs"",
                    jo.""LastHeartbeat"", jo.""DispatchRetryCount"", jo.""NextDispatchRetryAt"", jo.""CreatedAt"", NOW()
                FROM ""JobOccurrences"" jo
                INNER JOIN to_archive ta ON jo.""Id"" = ta.""Id""
                RETURNING ""Id""
            )
            SELECT 
                (SELECT COUNT(*) FROM inserted) as archived_occurrences,
                (SELECT COUNT(*) FROM archived_logs) as archived_logs";

        var result = await connection.QueryFirstOrDefaultAsync<(int, int)>(
            archiveSql,
            new { CutoffDate = cutoffDate, BatchSize = batchSize });

        // Delete occurrences after logs are archived
        await connection.ExecuteAsync($@"
            DELETE FROM ""JobOccurrences""
            WHERE ""Id"" IN (
                SELECT ""Id"" FROM ""JobOccurrences""
                WHERE ""Status"" IN ({statusFilter})
                AND (
                    (""EndTime"" IS NOT NULL AND ""EndTime"" < @CutoffDate)
                    OR (""EndTime"" IS NULL AND ""CreatedAt"" < @CutoffDate)
                )
                LIMIT @BatchSize
            )", new { CutoffDate = cutoffDate, BatchSize = batchSize });

        return result;
    }

    private static async Task CreateArchiveIndexesAsync(
        NpgsqlConnection connection,
        string archiveTableName,
        string archiveLogsTableName,
        IJobContext context)
    {
        context.LogInformation("  Creating indexes on archive tables...");

        try
        {
            // Indexes for JobOccurrences archive table
            // Index on JobId for job-based queries
            await connection.ExecuteAsync($@"
                CREATE INDEX IF NOT EXISTS ""IX_{archiveTableName}_JobId""
                ON ""{archiveTableName}"" (""JobId"")");

            // Index on CorrelationId for tracing
            await connection.ExecuteAsync($@"
                CREATE INDEX IF NOT EXISTS ""IX_{archiveTableName}_CorrelationId""
                ON ""{archiveTableName}"" (""CorrelationId"")");

            // Index on EndTime for date-based queries
            await connection.ExecuteAsync($@"
                CREATE INDEX IF NOT EXISTS ""IX_{archiveTableName}_EndTime""
                ON ""{archiveTableName}"" (""EndTime"")");

            // Indexes for JobOccurrenceLogs archive table
            // Index on OccurrenceId for joining with occurrences
            await connection.ExecuteAsync($@"
                CREATE INDEX IF NOT EXISTS ""IX_{archiveLogsTableName}_OccurrenceId""
                ON ""{archiveLogsTableName}"" (""OccurrenceId"")");

            // Index on Timestamp for time-based queries
            await connection.ExecuteAsync($@"
                CREATE INDEX IF NOT EXISTS ""IX_{archiveLogsTableName}_Timestamp""
                ON ""{archiveLogsTableName}"" (""Timestamp"")");

            context.LogInformation("  [OK] Indexes created");
        }
        catch (Exception ex)
        {
            context.LogWarning($"  [WARNING] Failed to create indexes: {ex.Message}");
        }
    }

    private static async Task<long> GetTableSizeAsync(NpgsqlConnection connection, string table)
    {
        try
        {
            return await connection.ExecuteScalarAsync<long>(
                "SELECT pg_total_relation_size(@TableName)",
                new { TableName = table });
        }
        catch
        {
            return 0;
        }
    }

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        double size = bytes;

        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }

        return $"{size:0.##} {sizes[order]}";
    }
}
