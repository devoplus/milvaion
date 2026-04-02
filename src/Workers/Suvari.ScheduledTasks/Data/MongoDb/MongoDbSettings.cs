using MongoDB.Driver;
using RabbitMQ.Client;
using Suvari.ScheduledTasks.Core.Utilities;
using Suvari.ScheduledTasks.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Suvari.ScheduledTasks.Data.MongoDb;

/// <summary>
/// Merkezi ayar mekanizması için kullanılan helper.
/// </summary>
public class MongoDbSettings
{
    /// <summary>
    /// DB'den bir ayarı okumak için kullanılır.
    /// </summary>
    /// <param name="key">Ayar adı</param>
    /// <returns>Ayar değeri</returns>
    public static dynamic ReadSetting(string key)
    {
        try
        {
            var mongo = new MongoClient(SettingsExtensions.Default.MongoConnectionString);
            IMongoDatabase queueDb = mongo.GetDatabase(SettingsExtensions.Default.SettingsDbName);
            var collection = queueDb.GetCollection<Settings>("Settings");

            return collection.Find(t => t.Key == key).First().Value;
        }
        catch (Exception ex)
        {
            Exceptions.NewException(ex);
            return null;
        }
    }

    /// <summary>
    /// DB'ye ayar değeri yazmak için kullanılır.
    /// </summary>
    /// <param name="setting">Ayar bilgileri</param>
    /// <returns>İşlem sonucu başarılıysa true, değilse false döndürür.</returns>
    public static bool WriteSetting(Settings setting)
    {
        try
        {
            var mongo = new MongoClient(SettingsExtensions.Default.MongoConnectionString);
            IMongoDatabase queueDb = mongo.GetDatabase(SettingsExtensions.Default.SettingsDbName);
            var collection = queueDb.GetCollection<Settings>("Settings");

            collection.InsertOne(setting);
            return true;
        }
        catch (Exception ex)
        {
            Exceptions.NewException(ex);
            return false;
        }
    }
}
