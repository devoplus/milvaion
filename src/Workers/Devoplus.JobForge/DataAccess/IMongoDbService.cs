using MongoDB.Driver;

namespace Devoplus.JobForge.DataAccess;

public interface IMongoDbService
{
    IMongoCollection<T> GetCollection<T>();
    IMongoCollection<T> GetCollection<T>(string collectionName);
    IMongoCollection<T> GetMailQueueCollection<T>();
    IMongoCollection<T> GetMailQueueCollection<T>(string collectionName);
}
