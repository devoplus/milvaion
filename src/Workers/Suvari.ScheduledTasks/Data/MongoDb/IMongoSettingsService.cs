namespace Suvari.ScheduledTasks.Data.MongoDb;

public interface IMongoSettingsService
{
    Task<string> ReadSettingAsync(string key);
    Task<T> ReadSettingAsync<T>(string key);
}
