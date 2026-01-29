namespace Milvaion.Application.Dtos.AdminDtos;

/// <summary>
/// Database statistics response containing table sizes, occurrence growth, and large occurrences.
/// </summary>
public class DatabaseStatisticsDto
{
    /// <summary>
    /// Table size statistics.
    /// </summary>
    public List<TableSizeDto> TableSizes { get; set; }

    /// <summary>
    /// Total database size in bytes.
    /// </summary>
    public long TotalDatabaseSizeBytes { get; set; }

    /// <summary>
    /// Total database size (human-readable).
    /// </summary>
    public string TotalDatabaseSize { get; set; }

    /// <summary>
    /// Index efficiency statistics (unused/underutilized indexes).
    /// </summary>
    public IndexEfficiencyDto IndexEfficiency { get; set; }

    /// <summary>
    /// Database cache hit ratio (performance metric).
    /// </summary>
    public CacheHitRatioDto CacheHitRatio { get; set; }

    /// <summary>
    /// Table bloat detection (VACUUM recommendation).
    /// </summary>
    public TableBloatDto TableBloat { get; set; }
}

/// <summary>
/// Table size information.
/// </summary>
public class TableSizeDto
{
    /// <summary>
    /// Schema name (usually 'public').
    /// </summary>
    public string SchemaName { get; set; }

    /// <summary>
    /// Table name.
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Human-readable size (e.g., "6 GB").
    /// </summary>
    public string Size { get; set; }

    /// <summary>
    /// Size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Percentage of total database size.
    /// </summary>
    public decimal Percentage { get; set; }
}

/// <summary>
/// Index efficiency statistics.
/// </summary>
public class IndexEfficiencyDto
{
    /// <summary>
    /// List of unused or underutilized indexes.
    /// </summary>
    public List<IndexStatsDto> Indexes { get; set; }

    /// <summary>
    /// Total wasted space by unused indexes (bytes).
    /// </summary>
    public long TotalWastedBytes { get; set; }

    /// <summary>
    /// Total wasted space (human-readable).
    /// </summary>
    public string TotalWastedSpace { get; set; }

    /// <summary>
    /// Recommendation message.
    /// </summary>
    public string Recommendation { get; set; }
}

/// <summary>
/// Individual index statistics.
/// </summary>
public class IndexStatsDto
{
    /// <summary>
    /// Table name.
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Index name.
    /// </summary>
    public string IndexName { get; set; }

    /// <summary>
    /// Index size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Index size (human-readable).
    /// </summary>
    public string Size { get; set; }

    /// <summary>
    /// Number of index scans.
    /// </summary>
    public long Scans { get; set; }

    /// <summary>
    /// Number of tuples read.
    /// </summary>
    public long TuplesRead { get; set; }

    /// <summary>
    /// Efficiency score (0-100, higher is better).
    /// </summary>
    public decimal EfficiencyScore { get; set; }

    /// <summary>
    /// Status: "Unused", "Rarely Used", "Normal"
    /// </summary>
    public string Status { get; set; }
}

/// <summary>
/// Cache hit ratio statistics.
/// </summary>
public class CacheHitRatioDto
{
    /// <summary>
    /// Overall cache hit ratio (0-100).
    /// </summary>
    public decimal HitRatioPercentage { get; set; }

    /// <summary>
    /// Index cache hit ratio (0-100).
    /// </summary>
    public decimal IndexHitRatioPercentage { get; set; }

    /// <summary>
    /// Table cache hit ratio (0-100).
    /// </summary>
    public decimal TableHitRatioPercentage { get; set; }

    /// <summary>
    /// Number of blocks read from disk.
    /// </summary>
    public long DiskReads { get; set; }

    /// <summary>
    /// Number of blocks read from cache.
    /// </summary>
    public long CacheReads { get; set; }

    /// <summary>
    /// Performance status: "Excellent", "Good", "Poor", "Critical"
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Recommendation message.
    /// </summary>
    public string Recommendation { get; set; }
}

/// <summary>
/// Table bloat detection statistics.
/// </summary>
public class TableBloatDto
{
    /// <summary>
    /// List of bloated tables.
    /// </summary>
    public List<BloatedTableDto> BloatedTables { get; set; }

    /// <summary>
    /// Total wasted space by bloat (bytes).
    /// </summary>
    public long TotalWastedBytes { get; set; }

    /// <summary>
    /// Total wasted space (human-readable).
    /// </summary>
    public string TotalWastedSpace { get; set; }

    /// <summary>
    /// Recommendation message.
    /// </summary>
    public string Recommendation { get; set; }
}

/// <summary>
/// Individual bloated table statistics.
/// </summary>
public class BloatedTableDto
{
    /// <summary>
    /// Table name.
    /// </summary>
    public string TableName { get; set; }

    /// <summary>
    /// Actual size in bytes.
    /// </summary>
    public long ActualSizeBytes { get; set; }

    /// <summary>
    /// Actual size (human-readable).
    /// </summary>
    public string ActualSize { get; set; }

    /// <summary>
    /// Expected size in bytes (without bloat).
    /// </summary>
    public long ExpectedSizeBytes { get; set; }

    /// <summary>
    /// Expected size (human-readable).
    /// </summary>
    public string ExpectedSize { get; set; }

    /// <summary>
    /// Wasted space in bytes.
    /// </summary>
    public long WastedBytes { get; set; }

    /// <summary>
    /// Wasted space (human-readable).
    /// </summary>
    public string WastedSpace { get; set; }

    /// <summary>
    /// Bloat percentage (0-100).
    /// </summary>
    public decimal BloatPercentage { get; set; }

    /// <summary>
    /// Dead tuples count.
    /// </summary>
    public long DeadTuples { get; set; }

    /// <summary>
    /// Live tuples count.
    /// </summary>
    public long LiveTuples { get; set; }

    /// <summary>
    /// Last VACUUM time.
    /// </summary>
    public DateTime? LastVacuum { get; set; }

    /// <summary>
    /// Last ANALYZE time.
    /// </summary>
    public DateTime? LastAnalyze { get; set; }

    /// <summary>
    /// Status: "Critical", "Warning", "Normal"
    /// </summary>
    public string Status { get; set; }
}
