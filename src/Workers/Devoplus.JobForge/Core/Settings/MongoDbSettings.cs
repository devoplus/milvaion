namespace Devoplus.JobForge.Core.Settings;

public class MongoDbSettings
{
    public const string SectionKey = "MongoDB";

    public string ConnectionString { get; set; } = default!;
    public string DatabaseName { get; set; } = "SvrTech";
    public string MailQueueDatabaseName { get; set; } = "MailQueue";
}
