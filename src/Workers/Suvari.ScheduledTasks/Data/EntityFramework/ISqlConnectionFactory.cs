namespace Suvari.ScheduledTasks.Data.EntityFramework;

public enum SqlConnectionName
{
    Portal,
    SuvariPortal,
    Nebim,
    eBA,
    External
}

public interface ISqlConnectionFactory
{
    Kata GetConnection(SqlConnectionName name);
}
