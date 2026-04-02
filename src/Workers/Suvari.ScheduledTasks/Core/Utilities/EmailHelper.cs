using Suvari.ScheduledTasks.Data.MongoDb;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace Suvari.ScheduledTasks.Core.Utilities;

/// <summary>
/// E-posta işlemleri için kullanılan helperlar
/// </summary>
public class EmailHelper : IEmailHelper
{
    private readonly IMongoSettingsService _settings;

    public EmailHelper(IMongoSettingsService settings)
    {
        _settings = settings;
    }

    private async Task<SmtpClient> BuildSmtpClientAsync()
    {
        var host = await _settings.ReadSettingAsync("MailHost");
        if (string.IsNullOrWhiteSpace(host))
            throw new InvalidOperationException("SMTP ayarı bulunamadı: 'MailHost' MongoDB Settings koleksiyonunda tanımlı değil.");

        return new SmtpClient
        {
            Host        = host,
            Port        = await _settings.ReadSettingAsync<int>("MailPort"),
            Credentials = new System.Net.NetworkCredential(
                await _settings.ReadSettingAsync("MailUserName"),
                await _settings.ReadSettingAsync("MailPassword")),
            EnableSsl   = await _settings.ReadSettingAsync<bool>("SSL")
        };
    }

    private async Task<string> GetMailFromAsync()
        => await _settings.ReadSettingAsync("MailUserName");

    public async Task SendEmailAsync(string toEmail, string mailSubject, string mailContent, MailPriority mailPriority = MailPriority.Normal)
    {
        try
        {
            if (string.IsNullOrEmpty(toEmail))
                toEmail = "yazilim@suvari.com.tr";

            using var mail = await BuildSmtpClientAsync();
            var from = await GetMailFromAsync();

            var mesaj = new MailMessage
            {
                IsBodyHtml = true,
                Priority = mailPriority,
                From = new MailAddress(from)
            };
            mesaj.To.Add(toEmail);
            mesaj.Bcc.Add("yazilim@suvari.com.tr");
            mesaj.Subject = mailSubject;
            mesaj.Body = BodyReplace(mailContent);

            await mail.SendMailAsync(mesaj);
        }
        catch (Exception ex)
        {
            Exceptions.NewException(new Exception($"SendEmail Exception! To: {toEmail}, Subject: {mailSubject}", ex));
        }
    }
    /// <summary>
    /// E-posta göndermek için kullanılır.
    /// </summary>
    /// <param name="toEmail">E-posta gönderilecek adres.</param>
    /// <param name="mailSubject">E-posta başlığı</param>
    /// <param name="mailContent">E-posta HTML içeriği</param>
    public void SendEmail(string toEmail, string mailSubject, string mailContent, MailPriority mailPriority = MailPriority.Normal)
    {
        try
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                toEmail = "yazilim@suvari.com.tr";
            }

            SmtpClient mail = new SmtpClient
            {
                Host = MongoDbSettings.ReadSetting("MailHost"),
                Port = Convert.ToInt32(MongoDbSettings.ReadSetting("MailPort")),
                Credentials = new System.Net.NetworkCredential(MongoDbSettings.ReadSetting("MailUserName"), MongoDbSettings.ReadSetting("MailPassword")),
                EnableSsl = Convert.ToBoolean(MongoDbSettings.ReadSetting("SSL"))
            };

            MailMessage mesaj = new MailMessage
            {
                IsBodyHtml = true,
                Priority = mailPriority,
                From = new MailAddress(MongoDbSettings.ReadSetting("MailUserName"))
            };

            mesaj.To.Add(toEmail);
            mesaj.Bcc.Add("yazilim@suvari.com.tr");
            mesaj.Subject = mailSubject;
            mesaj.Body = BodyReplace(mailContent);

            mail.Send(mesaj);
        }
        catch (Exception ex)
        {
            Exceptions.NewException(new Exception($"SendEmail Exception! From: {MongoDbSettings.ReadSetting("MailUserName")}, To: {toEmail}, Subject: {mailSubject}", ex));
        }

    }

    /// <summary>
    /// E-posta ile toplantı daveti göndermek için kullanılır.
    /// </summary>
    /// <param name="organizatorEmail">Toplantı sahibine ait e-posta adresi</param>
    /// <param name="guestEmails">Katılımcı e-posta adresleri</param>
    /// <param name="Subject">Toplantı konusu</param>
    /// <param name="body">Toplantı açıklaması</param>
    /// <param name="meetingLocation">Toplantı konumu</param>
    /// <param name="startTime">Başlangıç tarih ve saati</param>
    /// <param name="endTime">Bitiş tarih ve saati</param>
    public void SendCalendarEvent(string organizatorEmail, string[] guestEmails, string subject, string body, string meetingLocation, DateTime startTime, DateTime endTime)
    {
        string participants = "";
        MailMessage msg = new MailMessage
        {
            From = new MailAddress(MongoDbSettings.ReadSetting("MailUserName"))
        };

        foreach (string email in guestEmails)
        {
            msg.To.Add(new MailAddress(email));
            participants += email + ",";
        }

        msg.CC.Add(new MailAddress(organizatorEmail));
        msg.Bcc.Add(new MailAddress("yazilim@suvari.com.tr", "Suvari Yazilim"));
        msg.ReplyTo = new MailAddress(organizatorEmail);
        msg.Subject = subject;
        msg.Body = body;
        msg.IsBodyHtml = true;
        msg.Headers.Add("Content-class", "urn:content-classes:calendarmessage");

        StringBuilder str = new StringBuilder();
        str.AppendLine("BEGIN:VCALENDAR");
        str.AppendLine("PRODID:-//Schedule a Meeting");
        str.AppendLine("VERSION:2.0");
        str.AppendLine("METHOD:REQUEST");
        str.AppendLine("BEGIN:VEVENT");
        str.AppendLine(string.Format("DTSTART:{0:yyyyMMddTHHmmssZ}", startTime));
        str.AppendLine(string.Format("DTSTAMP:{0:yyyyMMddTHHmmssZ}", DateTime.Now));
        str.AppendLine(string.Format("DTEND:{0:yyyyMMddTHHmmssZ}", endTime));
        str.AppendLine("LOCATION: " + meetingLocation);
        str.AppendLine(string.Format("UID:{0}", Guid.NewGuid()));
        str.AppendLine(string.Format("DESCRIPTION:{0}", msg.Body));
        str.AppendLine(string.Format("X-ALT-DESC;FMTTYPE=text/html:{0}", msg.Body));
        str.AppendLine(string.Format("SUMMARY:{0}", msg.Subject));
        str.AppendLine(string.Format("ORGANIZER:MAILTO:{0}", new MailAddress(organizatorEmail)));

        str.AppendLine("ATTENDEE;RSVP=TRUE;PARTSTAT=ACCEPTED;ROLE=REQ-PARTICIPANT:MAILTO:" + participants.Substring(0, participants.Length));

        str.AppendLine("BEGIN:VALARM");
        str.AppendLine("TRIGGER:-PT15M");
        str.AppendLine("ACTION:DISPLAY");
        str.AppendLine("DESCRIPTION:Reminder");
        str.AppendLine("END:VALARM");
        str.AppendLine("END:VEVENT");
        str.AppendLine("END:VCALENDAR");

        SmtpClient smtpclient = new SmtpClient
        {
            Host = MongoDbSettings.ReadSetting("MailHost"),
            Port = Convert.ToInt32(MongoDbSettings.ReadSetting("MailPort")),
            Credentials = new System.Net.NetworkCredential(MongoDbSettings.ReadSetting("MailUserName"), MongoDbSettings.ReadSetting("MailPassword")),
            EnableSsl = Convert.ToBoolean(MongoDbSettings.ReadSetting("SSL"))
        };

        System.Net.Mime.ContentType contype = new System.Net.Mime.ContentType("text/calendar");
        contype.Parameters.Add("method", "REQUEST");
        contype.Parameters.Add("name", "Meeting.ics");
        AlternateView avCal = AlternateView.CreateAlternateViewFromString(str.ToString(), contype);
        msg.AlternateViews.Add(avCal);
        smtpclient.Send(msg);
    }

    /// <summary>
    /// CC'li kişi olacak şekilde e-posta göndermek için kullanılır.
    /// </summary>
    /// <param name="toEmail">E-posta gönderilecek adresler</param>
    /// <param name="ccEmail">E-posta gönderilecek cc'de olacak adresler</param>
    /// <param name="mailSubject">E-posta konusu</param>
    /// <param name="mailContent">E-posta HTML içeriği</param>
    public void SendEmailWithCC(string[] toEmail, string[] ccEmail, string mailSubject, string mailContent)
    {
        try
        {
            SmtpClient mail = new SmtpClient
            {
                Host = MongoDbSettings.ReadSetting("MailHost"),
                Port = Convert.ToInt32(MongoDbSettings.ReadSetting("MailPort")),
                Credentials = new System.Net.NetworkCredential(MongoDbSettings.ReadSetting("MailUserName"), MongoDbSettings.ReadSetting("MailPassword")),
                EnableSsl = Convert.ToBoolean(MongoDbSettings.ReadSetting("SSL"))
            };

            MailMessage mesaj = new MailMessage
            {
                IsBodyHtml = true,
                Priority = MailPriority.High,
                From = new MailAddress(MongoDbSettings.ReadSetting("MailUserName"))
            };

            foreach (string to in toEmail)
            {
                if (!string.IsNullOrEmpty(to))
                {
                    mesaj.To.Add(to);
                }
            }

            foreach (string cc in ccEmail)
            {
                if (!string.IsNullOrEmpty(cc))
                {
                    mesaj.CC.Add(cc);
                }
            }

            mesaj.Bcc.Add("yazilim@suvari.com.tr");
            mesaj.Subject = mailSubject;
            mesaj.Body = BodyReplace(mailContent);

            mail.Send(mesaj);
        }
        catch (Exception ex)
        {
            Exceptions.NewException(ex);
        }

    }

    /// <summary>
    /// CC'li kişi olacak şekilde e-posta göndermek için kullanılır.
    /// </summary>
    /// <param name="toEmail">E-posta gönderilecek adres</param>
    /// <param name="ccEmail">E-posta gönderilecek cc'de olacak adresler</param>
    /// <param name="mailSubject">E-posta konusu</param>
    /// <param name="mailContent">E-posta HTML içeriği</param>
    public void SendEmailWithCC(string toEmail, string ccEmail, string mailSubject, string mailContent)
    {
        try
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                toEmail = "yazilim@suvari.com.tr";
            }

            if (string.IsNullOrEmpty(ccEmail))
            {
                ccEmail = "yazilim@suvari.com.tr";
            }

            SmtpClient mail = new SmtpClient
            {
                Host = MongoDbSettings.ReadSetting("MailHost"),
                Port = Convert.ToInt32(MongoDbSettings.ReadSetting("MailPort")),
                Credentials = new System.Net.NetworkCredential(MongoDbSettings.ReadSetting("MailUserName"), MongoDbSettings.ReadSetting("MailPassword")),
                EnableSsl = Convert.ToBoolean(MongoDbSettings.ReadSetting("SSL"))
            };

            MailMessage mesaj = new MailMessage
            {
                IsBodyHtml = true,
                Priority = MailPriority.High,
                From = new MailAddress(MongoDbSettings.ReadSetting("MailUserName"))
            };
            mesaj.To.Add(toEmail);
            mesaj.CC.Add(ccEmail);
            mesaj.Bcc.Add("yazilim@suvari.com.tr");
            mesaj.Subject = mailSubject;
            mesaj.Body = BodyReplace(mailContent);

            mail.Send(mesaj);
        }
        catch (Exception ex)
        {
            Exceptions.NewException(ex);
        }
    }

    /// <summary>
    /// Toplu e-posta göndermek için kullanılır.
    /// </summary>
    /// <param name="toMailList">E-posta gönderilecek adresler</param>
    /// <param name="mailSubject">E-posta konusu</param>
    /// <param name="mailContent">E-posta HTML içeriği</param>
    /// <returns>İşlem sonucu başarılıysa true, değilse false döndürür.</returns>
    public bool SendMailToList(List<string> toMailList, string mailSubject, string mailContent)
    {
        try
        {
            foreach (string mail in toMailList)
            {
                if (!string.IsNullOrEmpty(mail))
                {
                    SendEmail(mail, mailSubject, mailContent);
                    Thread.Sleep(1000);
                }
            }
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Dosya ile birlikte e-posta göndermek için kullanılır.
    /// </summary>
    /// <param name="toEmail">E-posta gönderilecek adres</param>
    /// <param name="mailSubject">E-posta konusu</param>
    /// <param name="mailContent">e-posta HTML içeriği</param>
    /// <param name="attachments">E-posta ile gönderilecek dosyalar</param>
    public void SendMailWithAttachment(string toEmail, string mailSubject, string mailContent, List<Attachment> attachments, string fromAddress = null)
    {
        try
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                toEmail = "yazilim@suvari.com.tr";
            }

            if (string.IsNullOrEmpty(fromAddress))
            {
                fromAddress = MongoDbSettings.ReadSetting("MailUserName");
            }

            SmtpClient mail = new SmtpClient
            {
                Host = MongoDbSettings.ReadSetting("MailHost"),
                Port = Convert.ToInt32(MongoDbSettings.ReadSetting("MailPort")),
                Credentials = new System.Net.NetworkCredential(MongoDbSettings.ReadSetting("MailUserName"), MongoDbSettings.ReadSetting("MailPassword")),
                EnableSsl = Convert.ToBoolean(MongoDbSettings.ReadSetting("SSL"))
            };

            MailMessage mesaj = new MailMessage
            {
                IsBodyHtml = true,
                Priority = MailPriority.High,
                From = new MailAddress(fromAddress)
            };

            if (toEmail.Contains(","))
            {
                string[] emails = toEmail.Split(',');

                foreach (string email in emails)
                {
                    if (!string.IsNullOrEmpty(email))
                    {
                        mesaj.To.Add(email);
                    }
                }
            }
            else
            {
                mesaj.To.Add(toEmail);
            }

            mesaj.Bcc.Add("yazilim@suvari.com.tr");
            mesaj.Subject = mailSubject;
            mesaj.Body = BodyReplace(mailContent);
            foreach (Attachment attachment in attachments)
            {
                mesaj.Attachments.Add(attachment);
            }

            mail.Send(mesaj);
        }
        catch (Exception ex)
        {
            Exceptions.NewException(ex);
        }
    }

    /// <summary>
    /// Dosya ile birlikte e-posta göndermek için kullanılır.
    /// </summary>
    /// <param name="toEmail">E-posta gönderilecek adres</param>
    /// <param name="mailSubject">E-posta konusu</param>
    /// <param name="mailContent">e-posta HTML içeriği</param>
    /// <param name="attachments">E-posta ile gönderilecek dosyalar</param>
    /// <param name="cc">E-posta gönderilecek cc'de olacak adresler</param>
    public void SendMailWithAttachment(string toEmail, string mailSubject, string mailContent, List<Attachment> attachments, string[] cc)
    {
        try
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                toEmail = "yazilim@suvari.com.tr";
            }

            SmtpClient mail = new SmtpClient
            {
                Host = MongoDbSettings.ReadSetting("MailHost"),
                Port = Convert.ToInt32(MongoDbSettings.ReadSetting("MailPort")),
                Credentials = new System.Net.NetworkCredential(MongoDbSettings.ReadSetting("MailUserName"), MongoDbSettings.ReadSetting("MailPassword")),
                EnableSsl = Convert.ToBoolean(MongoDbSettings.ReadSetting("SSL"))
            };

            MailMessage mesaj = new MailMessage
            {
                IsBodyHtml = true,
                Priority = MailPriority.High,
                From = new MailAddress(MongoDbSettings.ReadSetting("MailUserName"))
            };
            mesaj.To.Add(toEmail);
            mesaj.Bcc.Add("yazilim@suvari.com.tr");
            mesaj.Subject = mailSubject;
            mesaj.Body = BodyReplace(mailContent);

            foreach (string ccmail in cc)
            {
                if (!string.IsNullOrEmpty(ccmail))
                {
                    mesaj.CC.Add(ccmail);
                }
            }

            foreach (Attachment attachment in attachments)
            {
                mesaj.Attachments.Add(attachment);
            }

            mail.Send(mesaj);
        }
        catch (Exception ex)
        {
            Exceptions.NewException(ex);
        }
    }

    /// <summary>
    /// Dosya ile birlikte e-posta göndermek için kullanılır.
    /// </summary>
    /// <param name="toEmail">E-posta gönderilecek adres</param>
    /// <param name="mailSubject">E-posta konusu</param>
    /// <param name="mailContent">e-posta HTML içeriği</param>
    /// <param name="attachments">E-posta ile gönderilecek dosyalar</param>
    /// <param name="cc">E-posta gönderilecek cc'de olacak adresler</param>
    public void SendMailWithAttachment(string toEmail, string mailSubject, string mailContent, List<Attachment> attachments, string[] cc, string[] bcc)
    {
        try
        {
            if (string.IsNullOrEmpty(toEmail))
            {
                toEmail = "yazilim@suvari.com.tr";
            }

            SmtpClient mail = new SmtpClient
            {
                Host = MongoDbSettings.ReadSetting("MailHost"),
                Port = Convert.ToInt32(MongoDbSettings.ReadSetting("MailPort")),
                Credentials = new System.Net.NetworkCredential(MongoDbSettings.ReadSetting("MailUserName"), MongoDbSettings.ReadSetting("MailPassword")),
                EnableSsl = Convert.ToBoolean(MongoDbSettings.ReadSetting("SSL"))
            };

            MailMessage mesaj = new MailMessage
            {
                IsBodyHtml = true,
                Priority = MailPriority.High,
                From = new MailAddress(MongoDbSettings.ReadSetting("MailUserName"))
            };
            mesaj.To.Add(toEmail);
            mesaj.Bcc.Add("yazilim@suvari.com.tr");
            mesaj.Subject = mailSubject;
            mesaj.Body = BodyReplace(mailContent);

            foreach (string ccmail in cc)
            {
                if (!string.IsNullOrEmpty(ccmail))
                {
                    mesaj.CC.Add(ccmail);
                }
            }

            foreach (string bccmail in bcc)
            {
                if (!string.IsNullOrEmpty(bccmail))
                {
                    mesaj.Bcc.Add(bccmail);
                }
            }

            foreach (Attachment attachment in attachments)
            {
                mesaj.Attachments.Add(attachment);
            }

            mail.Send(mesaj);
        }
        catch (Exception ex)
        {
            Exceptions.NewException(ex);
        }
    }

    /// <summary>
    /// Toplu dosyalı e-posta göndermek için kullanılır.
    /// </summary>
    /// <param name="toMailList">E-posta gönderilecek alıcılara ait e-posta adresleri</param>
    /// <param name="mailSubject">E-posta konusu</param>
    /// <param name="mailContent">E-posta HTML içeriği</param>
    /// <param name="attachments">E-posta ile gönderilecek dosyalar</param>
    public void SendMailToListWithAttachment(List<string> toMailList, string mailSubject, string mailContent, List<Attachment> attachments)
    {
        foreach (string mail in toMailList)
        {
            if (!string.IsNullOrEmpty(mail))
            {
                SendMailWithAttachment(mail, mailSubject, mailContent, attachments);
                Thread.Sleep(1500);
            }
        }
    }

    /// <summary>
    /// Toplu dosyalı e-posta göndermek için kullanılır.
    /// </summary>
    /// <param name="toMailList">E-posta gönderilecek alıcılara ait e-posta adresleri</param>
    /// <param name="mailSubject">E-posta konusu</param>
    /// <param name="mailContent">E-posta HTML içeriği</param>
    /// <param name="attachments">E-posta ile gönderilecek dosyalar</param>
    /// <param name="cc">E-posta gönderilecek cc'de olacak alıcılara ait e-posta adresleri</param>
    public void SendMailToListWithAttachment(List<string> toMailList, string mailSubject, string mailContent, List<Attachment> attachments, string[] cc)
    {
        foreach (string mail in toMailList)
        {
            if (!string.IsNullOrEmpty(mail))
            {
                SendMailWithAttachment(mail, mailSubject, mailContent, attachments, cc);
                Thread.Sleep(3500);
            }
        }
    }

    /// <summary>
    /// Toplu dosyalı e-posta göndermek için kullanılır.
    /// </summary>
    /// <param name="toMailList">E-posta gönderilecek alıcılara ait e-posta adresleri</param>
    /// <param name="mailSubject">E-posta konusu</param>
    /// <param name="mailContent">E-posta HTML içeriği</param>
    /// <param name="attachments">E-posta ile gönderilecek dosyalar</param>
    /// <param name="cc">E-posta gönderilecek cc'de olacak alıcılara ait e-posta adresleri</param>
    /// <param name="bcc">E-posta gönderilecek bcc'de olacak alıcılara ait e-posta adresleri</param>
    public void SendMailToListWithAttachment(List<string> toMailList, string mailSubject, string mailContent, List<Attachment> attachments, string[] cc, string[] bcc)
    {
        foreach (string mail in toMailList)
        {
            if (!string.IsNullOrEmpty(mail))
            {
                SendMailWithAttachment(mail, mailSubject, mailContent, attachments, cc, bcc);
                Thread.Sleep(3500);
            }
        }
    }

    /// <summary>
    /// Tek bir e-posta üzerinden toplu, dosyalı e-posta göndermek için kullanılır.
    /// </summary>
    /// <param name="toMailList">E-posta gönderilecek alıcılara ait e-posta adresleri</param>
    /// <param name="mailSubject">E-posta konusu</param>
    /// <param name="mailContent">E-posta HTML içeriği</param>
    /// <param name="attachments">E-posta ile gönderilecek dosyalar</param>
    /// <param name="cc">E-posta gönderilecek cc'de olacak alıcılara ait e-posta adresleri</param>
    public void SendMailToListWithAttachmentOneMail(List<string> toMailList, string mailSubject, string mailContent, List<Attachment> attachments, string[] cc, string[] bcc, List<string> replyToList = null, string fromAddress = null)
    {
        try
        {
            if (string.IsNullOrEmpty(fromAddress))
            {
                fromAddress = MongoDbSettings.ReadSetting("MailUserName");
            }

            SmtpClient mail = new SmtpClient
            {
                Host = MongoDbSettings.ReadSetting("MailHost"),
                Port = Convert.ToInt32(MongoDbSettings.ReadSetting("MailPort")),
                Credentials = new System.Net.NetworkCredential(MongoDbSettings.ReadSetting("MailUserName"), MongoDbSettings.ReadSetting("MailPassword")),
                EnableSsl = Convert.ToBoolean(MongoDbSettings.ReadSetting("SSL"))
            };

            MailMessage mesaj = new MailMessage
            {
                IsBodyHtml = true,
                Priority = MailPriority.High,
                From = new MailAddress(fromAddress)
                //From = new MailAddress(Settings.ReadSetting("MailUserName"))
            };

            try
            {
                if (replyToList != null)
                {
                    foreach (string address in replyToList)
                    {
                        mesaj.ReplyToList.Add(address);
                    }
                }
            }
            catch
            {

            }

            foreach (string mailAddress in toMailList)
            {
                if (!string.IsNullOrEmpty(mailAddress))
                {
                    mesaj.To.Add(mailAddress);
                }
            }

            mesaj.Bcc.Add("yazilim@suvari.com.tr");
            mesaj.Subject = mailSubject;
            mesaj.Body = BodyReplace(mailContent);

            foreach (string ccmail in cc)
            {
                if (!string.IsNullOrEmpty(ccmail))
                {
                    mesaj.CC.Add(ccmail);
                }
            }

            foreach (string bccmail in bcc)
            {
                if (!string.IsNullOrEmpty(bccmail))
                {
                    mesaj.Bcc.Add(bccmail);
                }
            }

            foreach (Attachment attachment in attachments)
            {
                mesaj.Attachments.Add(attachment);
            }

            mail.Send(mesaj);
        }
        catch (Exception ex)
        {
            Exceptions.NewException(ex);
        }
    }

    public void SendMailToListWithAttachmentOneMail(string mailFrom, List<string> toMailList, string mailSubject, string mailContent, List<Attachment> attachments, string[] cc, string[] bcc, List<string> replyToList = null, int retryCount = 0)
    {
        try
        {
            Dictionary<string, (string, string)> smtpParameters = new Dictionary<string, (string, string)>();
            smtpParameters.Add("report@suvari.com.tr", ("report_6bda6e4e710e@smtp-relay.devoplus.email", "7bf322d8-4180-4bee-bc0a"));
            smtpParameters.Add("sistem@suvari.com.tr", ("sistem_e5428eaadc17@smtp-relay.devoplus.email", "57ac9bd6-05db-4e49-9d2f-0ef808f54b70"));
            smtpParameters.Add("duyuru@suvari.com.tr", ("4e2834679a1b@smtp-relay.devoplus.email", "1c80abe7-fd31-4f7c-9347-8dbc545b14ea"));
            smtpParameters.Add("mutabakat@suvari.com.tr", ("2b08006d5aba@smtp-relay.devoplus.email", "0ee2ba01-43e0-4431-b7e8-852f7f178afc"));
            smtpParameters.Add("report@backandbond.com", ("report_d24e9e9b4a43@smtp-relay.devoplus.email", "2242d741-1d7b-40b6-80a7"));
            smtpParameters.Add("duyuru@backandbond.com", ("duyuru_ff604787c49e@smtp-relay.devoplus.email", "62ec8473-bec7-479c-af8d-ae9fda4900fd"));

            if (!smtpParameters.ContainsKey(mailFrom))
            {
                throw new Exception("Email account not found!");
            }

            var matchedParameter = smtpParameters[mailFrom];

            SmtpClient mail = new SmtpClient
            {
                Host = "smtp-relay.devoplus.email",
                Port = 587,
                Credentials = new System.Net.NetworkCredential(matchedParameter.Item1, matchedParameter.Item2),
                EnableSsl = false,
                Timeout = 10000
            };

            MailMessage mesaj = new MailMessage
            {
                IsBodyHtml = true,
                Priority = MailPriority.High,
                From = new MailAddress(mailFrom)
            };

            try
            {
                if (replyToList != null)
                {
                    foreach (string address in replyToList)
                    {
                        mesaj.ReplyToList.Add(address);
                    }
                }
            }
            catch
            {

            }

            foreach (string mailAddress in toMailList)
            {
                if (!string.IsNullOrEmpty(mailAddress))
                {
                    mesaj.To.Add(mailAddress);
                }
            }

            mesaj.Bcc.Add("yazilim@suvari.com.tr");
            mesaj.Subject = mailSubject;
            mesaj.Body = BodyReplace(mailContent);

            foreach (string ccmail in cc)
            {
                if (!string.IsNullOrEmpty(ccmail))
                {
                    mesaj.CC.Add(ccmail);
                }
            }

            foreach (string bccmail in bcc)
            {
                if (!string.IsNullOrEmpty(bccmail))
                {
                    mesaj.Bcc.Add(bccmail);
                }
            }

            foreach (Attachment attachment in attachments)
            {
                mesaj.Attachments.Add(attachment);
            }

            mail.Send(mesaj);
        }
        catch (Exception ex)
        {
            if (retryCount <= 3)
            {
                Integrations.Telegram.SendMessage($"To: {string.Join("; ", toMailList)}, CC: {string.Join("; ", cc)}, BCC: {string.Join("; ", bcc)} adreslerine {mailSubject} başlığıyla gönderilen aşağıdaki e-posta {ex.Message} hatası nedeniyle gönderilemedi. Yeniden deneme sayısı {retryCount}. Tekrar deneniyor.", Integrations.Telegram.Channel.ExceptionLogs);
                Exceptions.NewException(ex);

                SendMailToListWithAttachmentOneMail(mailFrom, toMailList, mailSubject, mailContent, attachments, cc, bcc, replyToList, retryCount + 1);
            }
            else
            {
                Integrations.Telegram.SendMessage($"To: {string.Join("; ", toMailList)}, CC: {string.Join("; ", cc)}, BCC: {string.Join("; ", bcc)} adreslerine {mailSubject} başlığıyla gönderilen aşağıdaki e-posta {ex.Message} hatası nedeniyle gönderilemedi. Yeniden deneme sayısı {retryCount}. Tekrar deneme limiti aşıldı.", Integrations.Telegram.Channel.ExceptionLogs);
                Exceptions.NewException(ex);
            }
        }
    }

    /// <summary>
    /// Tek bir e-posta üzerinden toplu, dosyalı e-posta göndermek için kullanılır.
    /// </summary>
    /// <param name="toMailList">E-posta gönderilecek alıcılara ait e-posta adresleri</param>
    /// <param name="mailSubject">E-posta konusu</param>
    /// <param name="mailContent">E-posta HTML içeriği</param>
    /// <param name="attachments">E-posta ile gönderilecek dosyalar</param>
    /// <param name="cc">E-posta gönderilecek cc'de olacak alıcılara ait e-posta adresleri</param>
    public void SendMailToListWithAttachmentOneMail(List<string> toMailList, string mailSubject, string mailContent, List<Attachment> attachments, string[] cc, List<string> replyToList = null)
    {
        try
        {
            SmtpClient mail = new SmtpClient
            {
                Host = MongoDbSettings.ReadSetting("MailHost"),
                Port = Convert.ToInt32(MongoDbSettings.ReadSetting("MailPort")),
                Credentials = new System.Net.NetworkCredential(MongoDbSettings.ReadSetting("MailUserName"), MongoDbSettings.ReadSetting("MailPassword")),
                EnableSsl = Convert.ToBoolean(MongoDbSettings.ReadSetting("SSL"))
            };

            MailMessage mesaj = new MailMessage
            {
                IsBodyHtml = true,
                Priority = MailPriority.High,
                From = new MailAddress(MongoDbSettings.ReadSetting("MailUserName"))
            };

            try
            {
                if (replyToList != null)
                {
                    foreach (string address in replyToList)
                    {
                        mesaj.ReplyToList.Add(address);
                    }
                }
            }
            catch
            {

            }

            foreach (string mailAddress in toMailList)
            {
                if (!string.IsNullOrEmpty(mailAddress))
                {
                    mesaj.To.Add(mailAddress);
                }
            }

            mesaj.Bcc.Add("yazilim@suvari.com.tr");
            mesaj.Subject = mailSubject;
            mesaj.Body = BodyReplace(mailContent);

            foreach (string ccmail in cc)
            {
                if (!string.IsNullOrEmpty(ccmail))
                {
                    mesaj.CC.Add(ccmail);
                }
            }

            foreach (Attachment attachment in attachments)
            {
                mesaj.Attachments.Add(attachment);
            }

            mail.Send(mesaj);
        }
        catch (Exception ex)
        {
            Exceptions.NewException(ex);
        }
    }

    /// <summary>
    /// Standart e-posta template'i ile mail göndermek için kullanılır.
    /// </summary>
    /// <param name="toEmail">E-posta gönderilecek adresler</param>
    /// <param name="ccEmail">E-posta gönderilecek cc'de olacak adresler</param>
    /// <param name="mailSubject">E-posta konusu</param>
    /// <param name="mailContent">E-posta HTML içeriği</param>
    public void SendEmailWithStandardTemplate(string[] toEmail, string[] ccEmail, string mailSubject, string templateName, string headerHtml, string bodyHtml, string footerHtml, string mailFrom = "")
    {
        SmtpClient mail = new SmtpClient
        {
            Host = MongoDbSettings.ReadSetting("MailHost"),
            Port = Convert.ToInt32(MongoDbSettings.ReadSetting("MailPort")),
            Credentials = new NetworkCredential(MongoDbSettings.ReadSetting("MailUserName"), MongoDbSettings.ReadSetting("MailPassword")),
            EnableSsl = Convert.ToBoolean(MongoDbSettings.ReadSetting("SSL"))
        };

        SendEmailWithStandardTemplate(toEmail, ccEmail, mailSubject, templateName, headerHtml, bodyHtml, footerHtml, mail, mailFrom);
    }

    public void SendEmailWithStandardTemplate(string[] toEmail, string[] ccEmail, string mailSubject, string templateName, string headerHtml, string bodyHtml, string footerHtml, SmtpClient smtpClient, string mailFrom = "")
    {
        try
        {
            MailMessage mesaj = new MailMessage
            {
                IsBodyHtml = true,
                From = new MailAddress(string.IsNullOrEmpty(mailFrom) ? MongoDbSettings.ReadSetting("MailUserName") : mailFrom)
            };

            foreach (string to in toEmail)
            {
                if (!string.IsNullOrEmpty(to))
                {
                    mesaj.To.Add(to);
                }
            }

            if (ccEmail != null && ccEmail.Length != 0)
            {
                foreach (string cc in ccEmail)
                {
                    mesaj.CC.Add(cc);
                }
            }

            mesaj.Bcc.Add("yazilim@suvari.com.tr");
            mesaj.Subject = mailSubject;

            WebClient wc = new WebClient();
            string baseHtml = wc.DownloadString(string.Format(BrandHelper.CDNUri + "Mailing/{0}/mailTemplate.html", templateName));
            string mailContent = baseHtml.Replace("%HEADER%", headerHtml).Replace("%BODY%", bodyHtml).Replace("%FOOTER%", footerHtml);

            mesaj.Body = BodyReplace(mailContent);
            smtpClient.Send(mesaj);
        }
        catch (Exception ex)
        {
            Exceptions.NewException(ex);
        }
    }

    public static void SendMailWithMessage(MailMessage message)
    {
        try
        {
            SmtpClient mail = new SmtpClient
            {
                Host = MongoDbSettings.ReadSetting("MailHost"),
                Port = Convert.ToInt32(MongoDbSettings.ReadSetting("MailPort")),
                Credentials = new System.Net.NetworkCredential(MongoDbSettings.ReadSetting("MailUserName"), MongoDbSettings.ReadSetting("MailPassword")),
                EnableSsl = Convert.ToBoolean(MongoDbSettings.ReadSetting("SSL"))
            };

            message.Bcc.Add("yazilim@suvari.com.tr");
            mail.Send(message);
        }
        catch (Exception ex)
        {
            Exceptions.NewException(ex);
        }
    }

    /// <summary>
    /// E-postada yer alan hatalı karakterleri düzeltmek için kullanılır.
    /// </summary>
    /// <param name="mailBody">Düzenlenmemiş mail içeriği</param>
    /// <returns>Düzenlenmiş mail içeriği</returns>
    private string BodyReplace(string mailBody)
    {
        // resimlerin ve yanliş karakterlerin düzgün görünmesi için gerekli
        mailBody = mailBody.Replace("&#60;", "<");
        mailBody = mailBody.Replace("&#62;", ">");
        mailBody = mailBody.Replace("&amp;", "&");
        mailBody = mailBody.Replace("&quot;", "");
        mailBody = mailBody.Replace("&lt;", "<");
        mailBody = mailBody.Replace("&gt;", ">");
        // resimlerin ve yanliş karakterlerin düzgün görünmesi için gerekli

        return mailBody;
    }
}