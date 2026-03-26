namespace ReporterWorker.Options;

public class ReporterOptions
{
    public const string SectionKey = "Reporter";

    public string DatabaseConnectionString { get; set; }

    public ReportGenerationSettings ReportGeneration { get; set; } = new();
}

public class ReportGenerationSettings
{
    public int DataRetentionDays { get; set; } = 30;

    public int LookbackHours { get; set; } = 24;

    public int TopNLimit { get; set; } = 10;

    public int MaxScheduleDeviations { get; set; } = 500;
}
