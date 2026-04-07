using Devoplus.JobForge.Entities.Queue;

namespace Devoplus.JobForge.DataAccess;

public interface IMailQueueService
{
    Task EnqueueAsync(EmailRequest emailRequest);
}
