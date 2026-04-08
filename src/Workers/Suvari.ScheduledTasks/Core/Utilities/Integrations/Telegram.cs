using MongoDB.Bson;
using MongoDB.Driver;
using RabbitMQ.Client;
using Suvari.ScheduledTasks.Data.MongoDb;
using Suvari.ScheduledTasks.Entities;
using Suvari.ScheduledTasks.Entities.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using Telegram.Bot;

namespace Suvari.ScheduledTasks.Core.Utilities.Integrations;

/// <summary>
/// Telegram API'larını tetikleyen entegrasyon
/// </summary>
public class Telegram
{
    private static MongoClient _mongo;
    private static MongoClient GetMongo()
    {
        if (_mongo != null)
            return _mongo;

        var connStr = SettingsExtensions.Default.MongoConnectionString;

        if (string.IsNullOrWhiteSpace(connStr))
            throw new InvalidOperationException("Telegram: MongoDB bağlantı dizesi boş — SettingsExtensions.Default.MongoConnectionString henüz yapılandırılmamış.");

        _mongo = new MongoClient(connStr);

        return _mongo;
    }

    /// <summary>
    /// Mevcut tanımlı kanallar
    /// </summary>
    public enum Channel : long
    {
        /// <summary>
        /// Hata logları için kullanılan Telegram kanalı
        /// </summary>
        ExceptionLogs = -458120039,
        /// <summary>
        /// Servise logları için kullanılan Telegram kanalı
        /// </summary>
        ServiceLogs = -443378856,
        /// <summary>
        /// WMS logları için kullanılan Telegram kanalı
        /// </summary>
        WMSLogs = -469469160,
        /// <summary>
        /// Nebim Web Integrator Service logları için kullanılan Telegram kanalı
        /// </summary>
        NebimIntegratorLogs = -425773325,
        /// <summary>
        /// PDKS logları için kullanılan Telegram kanalı
        /// </summary>
        PDKSLogs = -475114961,
        /// <summary>
        /// Figensoft SMS gönderim logları için kullanılan Telegram kanalı
        /// </summary>
        SMSLogs = -416624131,
        /// <summary>
        /// Kritik alarmları göndermek için kullanılan Telegram kanalı
        /// </summary>
        CriticalAlerts = -503838893,
        /// <summary>
        /// Sunucu bildirimleri için kullanılan Telegram kanalı
        /// </summary>
        ServerLogs = -621279604
    }

    /// <summary>
    /// Telegramda belirtilen gruba mesaj atmak için kullanılır.
    /// </summary>
    /// <param name="message">Gönderilecek Mesaj</param>
    /// <param name="channel">Kanal</param>
    /// <param name="disableNotification">Gönderilen mesaj için kullanıcıya bildirim gösterilmeyecekse true gönderilir.</param>
    public static void SendMessage(string message, Channel channel, bool disableNotification = false)
    {
        try
        {
            IMongoDatabase logQueueDb = GetMongo().GetDatabase(SettingsExtensions.Default.LogQueueDbName);
            var collection = logQueueDb.GetCollection<LogQueue>("LogQueue");

            collection.InsertOne(new LogQueue
            {
                _id = ObjectId.GenerateNewId(),
                Created = DateTime.Now.AddHours(3),
                State =QueueState.Waiting,
                RetryCount = 0,
                Message = $"Brand: {Globals.CurrentBrand}{Environment.NewLine}Server: {Environment.MachineName}{Environment.NewLine}{Environment.NewLine}{message}",
                Channel = (long)channel,
                DisableNotification = disableNotification,
                IsHighPriority = false
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine(Exceptions.FlattenException(ex));
            //Helpers.Exceptions.NewException(ex);
        }
    }

    public static async Task SendMessageDirectly(string botToken, string message, long channel, bool disableNotification = false)
    {
        try
        {
            TelegramBotClient Bot = new TelegramBotClient(botToken);

            if (message.Length > 4000)
            {
                double totalParts = message.Length / 4000;
                for (int i = 0; i < Math.Ceiling(totalParts); i++)
                {
                    await Bot.SendMessage(channel.ToString(), message.Substring(i * 4000, 4000), disableNotification: disableNotification);
                }
            }
            else
            {
                await Bot.SendMessage(channel.ToString(), message, disableNotification: disableNotification);
            }
        }
        catch (Exception ex)
        {
            Exceptions.NewException(ex, false, false);
        }
    }
}