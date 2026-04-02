using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using SqlKata;
using Suvari.ScheduledTasks.Core;
using Suvari.ScheduledTasks.Core.Integrations.REMVision;
using Suvari.ScheduledTasks.Core.Utilities;
using Suvari.ScheduledTasks.Data.EntityFramework;
using Suvari.ScheduledTasks.Data.MongoDb;
using Suvari.ScheduledTasks.Entities;
using System.Data;

namespace Suvari.ScheduledTasks.Jobs;

public class REMVision(
    ISqlConnectionFactory sqlFactory,
    IMongoSettingsService mongoSettings,
    IEmailHelper emailHelper) : IAsyncJob
{
    private const string _serviceName = "REMVision";

    private readonly ISqlConnectionFactory _sqlFactory = sqlFactory;
    private readonly IMongoSettingsService _mongoSettings = mongoSettings;
    private readonly IEmailHelper _emailHelper = emailHelper;

    public async Task ExecuteAsync(IJobContext context)
    {
        context.LogInformation($"🚀 {_serviceName} basladi.");

        try
        {
            context.LogInformation("🔌 Nebim veritabanı bağlantısı kuruluyor...");
            var coskunDB = _sqlFactory.GetConnection(SqlConnectionName.Nebim);
            context.LogInformation("✅ Nebim veritabanı bağlantısı kuruldu.");

            Udentify udentify = Globals.CurrentBrand switch
            {
                Brand.BackAndBond => new Udentify("apiuser@suvari.com", "suvari123", 208),
                Brand.Suvari => new Udentify("apiuser@suvari.com", "suvari123", 209),
                _ => null
            };

            if (udentify != null)
            {
                context.LogInformation($"🏪 Udentify mağazaları çekiliyor... (Marka: {Globals.CurrentBrand})");
                var stores = udentify.GetStores();
                context.LogInformation($"📋 {stores.Count} mağaza bulundu.");

                foreach (var store in stores)
                {
                    context.LogInformation($"🏬 Mağaza işleniyor: {store.Name} (PartnerId: {store.PartnerId}, Id: {store.Id})");

                    foreach (var date in EachDay(DateTime.Now.AddDays(-3), DateTime.Now))
                    {
                        context.LogInformation($"📅 {store.PartnerId} mağazası için {date:dd.MM.yyyy} tarihli giriş sayıları çekiliyor...");
                        var enteranceCounts = udentify.GetLineEntranceCount(store.Id, StartOfDay(date), EndOfDay(date), 180);

                        if (enteranceCounts.Success && !string.IsNullOrEmpty(store.PartnerId))
                        {
                            store.PartnerId = store.PartnerId.ToUpper();
                            var enteranceData = enteranceCounts.Data.FirstOrDefault(t => t.Name == "Entrance");

                            if (enteranceData != null)
                            {
                                context.LogInformation($"🔍 {store.PartnerId} ofisi Nebim'de kontrol ediliyor...");
                                var checkOfficeCode = coskunDB.ExecuteReader(new Query("cdOffice").Where("OfficeCode", store.PartnerId));

                                if (checkOfficeCode.Rows.Count > 0)
                                {
                                    context.LogInformation($"💾 {store.PartnerId} | {date:dd.MM.yyyy} → {enteranceData.Serial.Length} saatlik veri yazılıyor...");
                                    int yazilan = 0;

                                    for (int i = 0; i < enteranceData.Serial.Length; i++)
                                    {
                                        if (enteranceData.Serial[i] == null || Convert.ToInt32(enteranceData.Serial[i]) == 0)
                                            continue;

                                        coskunDB.ExecuteNonQuery(new Query("trStoreVisitors")
                                            .Where("StoreCode", store.PartnerId)
                                            .Where("CurrentDate", date)
                                            .Where("CurrentHour", i + 1)
                                            .AsDelete(), CommandType.Text);

                                        var insertQuery = new Query("trStoreVisitors").AsInsert(new
                                        {
                                            CompanyCode = checkOfficeCode.Rows[0]["CompanyCode"],
                                            OfficeCode = store.PartnerId,
                                            StoreTypeCode = 5,
                                            StoreCode = store.PartnerId,
                                            CurrentDate = date,
                                            CurrentHour = i + 1,
                                            InVisitorCount = enteranceData.Serial[i],
                                            OutVisitorCount = enteranceData.Serial[i],
                                            CreatedUserName = "RV-Udentify",
                                            CreatedDate = DateTime.Now,
                                            LastUpdatedUserName = "RV-Udentify",
                                            LastUpdatedDate = DateTime.Now,
                                            RowGuid = Guid.NewGuid()
                                        });

                                        if (coskunDB.ExecuteNonQuery(insertQuery, CommandType.Text) == 0)
                                        {
                                            context.LogInformation($"⚠️ {store.PartnerId} | {date:dd.MM.yyyy} | Saat {i + 1} verisi yazılamadı, e-posta gönderiliyor.");
                                            string writeErrorMsg = $"RemVision Entegrasyonu {DateTime.Now:dd.MM.yyyy HH:mm} tarihinde çalışırken {i + 1}. saat için kişi sayım verisi içeren {store.PartnerId} ofisinin verisini yazamadı. Lütfen aksiyon alınız.";
                                            await _emailHelper.SendEmailAsync("yazilim@suvari.com.tr", "RemVision Entegrasyonu (v2) Hata Bilgilendirmesi", writeErrorMsg);
                                            await _emailHelper.SendEmailAsync("bilgisistemleri@suvari.com.tr", "RemVision Entegrasyonu (v2) Hata Bilgilendirmesi", writeErrorMsg);
                                        }
                                        else
                                        {
                                            yazilan++;
                                        }
                                    }

                                    context.LogInformation($"✅ {store.PartnerId} | {date:dd.MM.yyyy} → {yazilan} saatlik kayıt başarıyla yazıldı.");
                                }
                                else
                                {
                                    context.LogInformation($"❌ {store.PartnerId} ofisi Nebim'de bulunamadı, e-posta gönderiliyor.");
                                    string notFoundMsg = $"RemVision Entegrasyonu {DateTime.Now:dd.MM.yyyy HH:mm} tarihinde çalışırken kişi sayım verisi içeren {store.PartnerId} ofisini bulamadığı için işlem yapamıyor. Lütfen aksiyon alınız.";
                                    await _emailHelper.SendEmailAsync("yazilim@suvari.com.tr", "Rem Vision Udentify Entegrasyonu Hata Bilgilendirmesi", notFoundMsg);
                                    await _emailHelper.SendEmailAsync("bilgisistemleri@suvari.com.tr", "Rem Vision Udentify Entegrasyonu Hata Bilgilendirmesi", notFoundMsg);
                                }
                            }
                            else
                            {
                                context.LogInformation($"ℹ️ {store.PartnerId} | {date:dd.MM.yyyy} → 'Entrance' verisi bulunamadı, atlanıyor.");
                            }
                        }
                        else
                        {
                            context.LogInformation($"⚠️ {store.PartnerId} | {date:dd.MM.yyyy} → Udentify yanıtı başarısız veya PartnerId boş, atlanıyor.");
                        }
                    }

                    context.LogInformation($"🏁 Mağaza tamamlandı: {store.PartnerId}");
                }
            }
            else
            {
                context.LogInformation($"⚠️ Marka tanımsız ({Globals.CurrentBrand}), Udentify örneği oluşturulamadı.");
            }

            string complationMsg = $"RemVision Entegrasyonu {DateTime.Now:dd.MM.yyyy HH:mm} tarihinde tamamlandı.";
            await _emailHelper.SendEmailAsync("yazilim@suvari.com.tr", "Rem Vision Udentify Entegrasyonu Tamamlanma Bilgilendirmesi", complationMsg);
            context.LogInformation($"✅ {_serviceName} tamamlandi.");
        }
        catch (Exception ex)
        {
            context.LogError($"{_serviceName} hata aldi: {ex.Message}");

            await _emailHelper.SendEmailAsync(
                "yazilim@suvari.com.tr",
                "Zamanlanmis Gorev Bilgilendirmesi",
                $"{_serviceName} {DateTime.Now:dd.MM.yyyy HH:mm:ss} tarihinde hata aldi.<br/><br/>{ex.Message}<br/><br/>{ex.StackTrace}");
            throw;
        }
    }

    private static IEnumerable<DateTime> EachDay(DateTime from, DateTime to)
    {
        for (var day = from.Date; day <= to.Date; day = day.AddDays(1))
            yield return day;
    }

    private static DateTime StartOfDay(DateTime date) => date.Date;

    private static DateTime EndOfDay(DateTime date) => date.Date.AddDays(1).AddTicks(-1);
}
