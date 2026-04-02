namespace Suvari.ScheduledTasks.Options;

public class SqlConnectionsOptions
{
    public const string SectionKey = "SqlConnections";

    public string Portal { get; set; }
    public string SuvariPortal { get; set; }
    public string Nebim { get; set; }
    public string EBA { get; set; }
    public string External { get; set; }
}
