using System.Net.Mail;

namespace Suvari.ScheduledTasks.Core.Utilities;

public interface IEmailHelper
{
    Task SendEmailAsync(string toEmail, string mailSubject, string mailContent, MailPriority mailPriority = MailPriority.Normal);
}
