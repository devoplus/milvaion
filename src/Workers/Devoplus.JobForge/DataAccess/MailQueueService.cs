using Devoplus.JobForge.Entities.Queue;
using MongoDB.Bson;

namespace Devoplus.JobForge.DataAccess;

public class MailQueueService : IMailQueueService
{
    private readonly IMongoDbService _mongoDbService;

    public MailQueueService(IMongoDbService mongoDbService)
    {
        _mongoDbService = mongoDbService;
    }

    public Task EnqueueAsync(EmailRequest emailRequest)
    {
        var collection = _mongoDbService.GetMailQueueCollection<MailQueueDocument>("Queue");

        var document = new MailQueueDocument
        {
            _id = ObjectId.GenerateNewId(),
            Status = QueueStatus.Waiting,
            InputParameters = emailRequest,
            StatusHistory =
            [
                new QueueStatusHistory
                {
                    CurrentStatus = QueueStatus.Waiting,
                    CreatedDate = DateTime.Now,
                    MachineName = Environment.MachineName
                }
            ],
            CreatedDate = DateTime.Now,
            DebugMode = false,
            MachineName = Environment.MachineName,
            MaxRetryCount = 3,
            RetryCount = 0,
            Type = QueueType.Email,
            Priority = QueuePriority.Low
        };

        collection.InsertOne(document);

        return Task.CompletedTask;
    }
}
