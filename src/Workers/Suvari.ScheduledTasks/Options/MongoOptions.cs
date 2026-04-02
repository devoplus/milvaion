namespace Suvari.ScheduledTasks.Options;

public class MongoOptions
{
    public const string SectionKey = "MongoDB";

    public string ConnectionString { get; set; }
    public string SettingsDbName { get; set; } = "Settings";
    public string LogQueueDbName { get; set; } = "LogQueue";
    public string LogDbName { get; set; } = "Logs";
}
