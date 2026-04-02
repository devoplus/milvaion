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

        // ── MongoDB bağlantı tanısı ───────────────────────────────────────────
        var _diagConnStr = SettingsExtensions.Default.MongoConnectionString;
        var _diagDbName  = SettingsExtensions.Default.SettingsDbName ?? "Settings";
        Console.WriteLine($"[DIAG] UseFileBasedSettings={suvariOptions.UseFileBasedSettings}");
        Console.WriteLine($"[DIAG] MongoConnStr={(string.IsNullOrWhiteSpace(_diagConnStr) ? "BOŞ/NULL ❌" : _diagConnStr[..Math.Min(40, _diagConnStr.Length)] + "...")}");
        Console.WriteLine($"[DIAG] SettingsDbName={_diagDbName}");

        if (!string.IsNullOrWhiteSpace(_diagConnStr))
        {
            try
            {
                var _diagClient    = new MongoClient(_diagConnStr);
                var _diagDatabases = _diagClient.ListDatabaseNames().ToList();
                Console.WriteLine($"[DIAG] MongoDB bağlantısı ✅ — Veritabanları: {string.Join(", ", _diagDatabases)}");

                // Her database'deki Settings koleksiyonunu tara — doğru olanı bul
                foreach (var dbName in _diagDatabases)
                {
                    try
                    {
                        var _chkCol   = _diagClient.GetDatabase(dbName).GetCollection<BsonDocument>("Settings");
                        var _chkCount = _chkCol.CountDocuments(FilterDefinition<BsonDocument>.Empty);
                        if (_chkCount > 0)
                        {
                            Console.WriteLine($"[DIAG] '{dbName}'.Settings → {_chkCount} kayıt ✅");
                            var _chkSample = _chkCol.Find(FilterDefinition<BsonDocument>.Empty).Limit(10).ToList();
                            foreach (var d in _chkSample)
                            {
                                var k = d.Contains("Key")  ? d["Key"].ToString()
                                      : d.Contains("key")  ? d["key"].ToString() : "?";
                                Console.WriteLine($"    → [{k}]");
                            }
                        }
                    }
                    catch { }
                }

                var _diagDbActual = _diagDatabases.FirstOrDefault(d => string.Equals(d, _diagDbName, StringComparison.OrdinalIgnoreCase)) ?? _diagDbName;
                var _diagCol      = _diagClient.GetDatabase(_diagDbActual).GetCollection<BsonDocument>("Settings");
                var _diagAll      = _diagCol.Find(FilterDefinition<BsonDocument>.Empty).ToList();
                Console.WriteLine($"[DIAG] Yapılandırılmış DB '{_diagDbActual}'.Settings — {_diagAll.Count} kayıt");
            }
            catch (Exception _diagEx)
            {
                Console.WriteLine($"[DIAG] MongoDB bağlantı hatası ❌ — {_diagEx.Message}");
            }
        }
        // ─────────────────────────────────────────────────────────────────────

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

            Console.WriteLine($"[DI:SQL] connStr={(mongoConnStr?.Length > 20 ? mongoConnStr[..20] + "..." : mongoConnStr ?? "NULL")}, db={dbName}");

            if (string.IsNullOrWhiteSpace(mongoConnStr))
            {
                Console.WriteLine("[DI:SQL] MongoDB bağlantı dizesi boş — SQL bağlantıları okunamadı.");
                return;
            }

            var collection = new MongoClient(mongoConnStr)
                .GetDatabase(dbName)
                .GetCollection<BsonDocument>("Settings");

            // Tanısal: mevcut key listesi — gerçek key adlarını görmek için
            var allKeys = collection
                .Find(FilterDefinition<BsonDocument>.Empty)
                .Project(Builders<BsonDocument>.Projection.Include("key").Include("Key"))
                .ToList();
            Console.WriteLine($"[DI:SQL] Settings'te {allKeys.Count} kayıt. Key listesi:");
            foreach (var k in allKeys)
            {
                var kv = k.Contains("key") ? k["key"].ToString() : k.Contains("Key") ? k["Key"].ToString() : "?";
                Console.WriteLine($"  → {kv}");
            }

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
                {
                    Console.WriteLine($"[DI:SQL] '{key}' bulunamadı.");
                    return null;
                }
                var value = doc.Contains("value") ? doc["value"]
                          : doc.Contains("Value") ? doc["Value"] : BsonNull.Value;
                return value == BsonNull.Value ? null : value.ToString();
            }

            opts.Portal       = Read("Portal.ConnectionString");
            opts.SuvariPortal = Read("SuvariPortal.ConnectionString");
            opts.Nebim        = Read("ConnectionStringNebim");
            opts.EBA          = Read("eBA.ConnectionString");
            opts.External     = Read("ExternalProjects.ConnectionString");

            Console.WriteLine($"[DI:SQL] Nebim={(opts.Nebim?.Length > 20 ? opts.Nebim[..20] + "..." : opts.Nebim ?? "NULL")}");
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
