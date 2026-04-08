using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;
using Suvari.ScheduledTasks.Core;
using Suvari.ScheduledTasks.Core.Utilities;
using Suvari.ScheduledTasks.Data.EntityFramework;
using Suvari.ScheduledTasks.Data.MongoDb;
using Suvari.ScheduledTasks.Options;

namespace Suvari.ScheduledTasks;

public static class DependencyInjection
{
    public static IServiceCollection AddSuvariServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Options
        var suvariOptions = configuration.GetSection(SuvariOptions.SectionKey).Get<SuvariOptions>() ?? new SuvariOptions();

        services.Configure<SuvariOptions>(configuration.GetSection(SuvariOptions.SectionKey));
        services.Configure<MongoOptions>(configuration.GetSection(MongoOptions.SectionKey));
        services.Configure<SqlConnectionsOptions>(configuration.GetSection(SqlConnectionsOptions.SectionKey));

        // Globals (statik miras uyumu için)
        Globals.CurrentBrand = suvariOptions.Brand;

        // SettingsExtensions'ı Docker/Windows moduna göre başlat
        SettingsExtensions.Initialize(configuration, suvariOptions.UseFileBasedSettings);

        // UseFileBasedSettings=true iken MongoOptions, appsettings'te boş kalır;
        // SettingsExtensions.Default üzerinden doldur ki IMongoSettingsService çalışsın
        services.PostConfigure<MongoOptions>(opts =>
        {
            if (string.IsNullOrWhiteSpace(opts.ConnectionString))
                opts.ConnectionString = SettingsExtensions.Default.MongoConnectionString;

            // SettingsDbName: MongoOptions class default değeri "Settings" olduğu için
            // IsNullOrWhiteSpace kontrolü hiçbir zaman true olmaz; her zaman Default'tan oku.
            if (!string.IsNullOrWhiteSpace(SettingsExtensions.Default.SettingsDbName))
                opts.SettingsDbName = SettingsExtensions.Default.SettingsDbName;
        });

        // SQL connection string'leri MongoDB Settings koleksiyonundan oku.
        // Öncelik: SettingsExtensions.Default (file-based Windows) → configuration (Docker/env var / appsettings)
        services.PostConfigure<SqlConnectionsOptions>(opts =>
        {
            var mongoConnStr = !string.IsNullOrWhiteSpace(SettingsExtensions.Default.MongoConnectionString)
                ? SettingsExtensions.Default.MongoConnectionString
                : configuration["MongoDB:ConnectionString"];

            var dbName = !string.IsNullOrWhiteSpace(SettingsExtensions.Default.SettingsDbName)
                ? SettingsExtensions.Default.SettingsDbName
                : configuration["MongoDB:SettingsDbName"] ?? "Settings";

            if (string.IsNullOrWhiteSpace(mongoConnStr))
                return;

            try
            {
                var collection = new MongoClient(mongoConnStr)
                    .GetDatabase(dbName)
                    .GetCollection<BsonDocument>("Settings");

                string Read(string key)
                {
                    var filter = Builders<BsonDocument>.Filter.Or(
                        Builders<BsonDocument>.Filter.Eq("key",  key),
                        Builders<BsonDocument>.Filter.Eq("Key",  key),
                        Builders<BsonDocument>.Filter.Eq("name", key),
                        Builders<BsonDocument>.Filter.Eq("Name", key)
                    );
                    var doc = collection.Find(filter).FirstOrDefault();
                    if (doc == null)
                        return null;
                    var value = doc.Contains("value") ? doc["value"]
                              : doc.Contains("Value") ? doc["Value"] : BsonNull.Value;
                    return value == BsonNull.Value ? null : value.ToString();
                }

                opts.Portal       = Read("Portal.ConnectionString");
                opts.SuvariPortal = Read("SuvariPortal.ConnectionString");
                opts.Nebim        = Read("ConnectionStringNebim");
                opts.EBA          = Read("eBA.ConnectionString");
                opts.External     = Read("ExternalProjects.ConnectionString");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Suvari.ScheduledTasks] MongoDB'dan SQL bağlantı dizeleri okunamadı: {ex.Message}");
            }
        });

        // MongoDB
        services.AddSingleton<IMongoSettingsService, MongoSettingsService>();

        // SQL bağlantı factory
        services.AddSingleton<ISqlConnectionFactory, SqlConnectionFactory>();

        // Email
        services.AddScoped<IEmailHelper, EmailHelper>();

        return services;
    }
}
