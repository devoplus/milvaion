using Devoplus.JobForge.Core.Settings;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Devoplus.JobForge.DataAccess;

public class MongoDbService : IMongoDbService
{
    private static bool _serializerRegistered;
    private static readonly object _serializerLock = new();

    private readonly MongoClient _mongoClient;
    private readonly IMongoDatabase _database;
    private readonly IMongoDatabase _mailQueueDatabase;

    public MongoDbService(MongoDbSettings settings)
    {
        lock (_serializerLock)
        {
            if (!_serializerRegistered)
            {
                var objectSerializer = new ObjectSerializer(type =>
                    ObjectSerializer.DefaultAllowedTypes(type) ||
                    (type.FullName != null && type.FullName.StartsWith("SvrTech")) ||
                    (type.FullName != null && type.FullName.StartsWith("Devoplus")));

                BsonSerializer.RegisterSerializer(objectSerializer);
                _serializerRegistered = true;
            }
        }

        _mongoClient = new MongoClient(settings.ConnectionString);
        _database = _mongoClient.GetDatabase(settings.DatabaseName.Replace(".", "_"));
        _mailQueueDatabase = _mongoClient.GetDatabase(settings.MailQueueDatabaseName.Replace(".", "_"));
    }

    public IMongoCollection<T> GetCollection<T>()
        => GetCollection<T>(typeof(T).Name.Split('`')[0]);

    public IMongoCollection<T> GetCollection<T>(string collectionName)
        => _database.GetCollection<T>(collectionName);

    public IMongoCollection<T> GetMailQueueCollection<T>()
        => GetMailQueueCollection<T>(typeof(T).Name.Split('`')[0]);

    public IMongoCollection<T> GetMailQueueCollection<T>(string collectionName)
        => _mailQueueDatabase.GetCollection<T>(collectionName);
}
//             var systemMailAddress = "koray.yilmaz@suvari.com.tr";
