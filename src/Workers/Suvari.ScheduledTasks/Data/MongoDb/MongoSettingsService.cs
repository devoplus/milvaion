using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Suvari.ScheduledTasks.Options;

namespace Suvari.ScheduledTasks.Data.MongoDb;

public class MongoSettingsService : IMongoSettingsService
{
    private readonly MongoOptions _opts;
    private IMongoCollection<BsonDocument> _collection;

    public MongoSettingsService(IOptions<MongoOptions> options)
    {
        _opts = options.Value;
        Console.WriteLine($"[MongoSettings] connStr={(_opts.ConnectionString?.Length > 20 ? _opts.ConnectionString[..20] + "..." : _opts.ConnectionString)}, db={_opts.SettingsDbName}");
    }

    private IMongoCollection<BsonDocument> GetCollection()
    {
        if (_collection != null)
            return _collection;

        if (string.IsNullOrWhiteSpace(_opts.ConnectionString))
            throw new InvalidOperationException("MongoDB bağlantı dizesi yapılandırılmamış. 'MongoDB:ConnectionString' ayarını kontrol edin.");

        var client  = new MongoClient(_opts.ConnectionString);
        var db      = client.GetDatabase(_opts.SettingsDbName);
        _collection = db.GetCollection<BsonDocument>("Settings");
        return _collection;
    }

    public async Task<string> ReadSettingAsync(string key)
    {
        var filter = Builders<BsonDocument>.Filter.Or(
            Builders<BsonDocument>.Filter.Eq("key", key),
            Builders<BsonDocument>.Filter.Eq("Key", key));

        var doc = await GetCollection().Find(filter).FirstOrDefaultAsync();
        Console.WriteLine($"[MongoSettings] key={key} → {(doc == null ? "null" : "bulundu")}");

        if (doc == null)
            return null;

        var value = doc.Contains("value") ? doc["value"]
                  : doc.Contains("Value") ? doc["Value"]
                  : BsonNull.Value;

        return value == BsonNull.Value ? null : value.ToString();
    }

    public async Task<T> ReadSettingAsync<T>(string key)
    {
        var value = await ReadSettingAsync(key);
        if (value == null)
            return default;
        return (T)Convert.ChangeType(value, typeof(T));
    }
}
