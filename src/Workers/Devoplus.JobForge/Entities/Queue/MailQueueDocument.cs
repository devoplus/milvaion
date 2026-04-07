using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Devoplus.JobForge.Entities.Queue;

public enum QueueStatus
{
    Waiting = 0,
    Processing = 1,
    Completed = 2,
    Error = 3
}

public enum QueueType
{
    Email = 0
}

public enum QueuePriority
{
    Low = 0,
    Normal = 1,
    High = 2
}

public class QueueStatusHistory
{
    public QueueStatus CurrentStatus { get; set; }
    public DateTime CreatedDate { get; set; }
    public string MachineName { get; set; } = default!;
}

public class MailQueueDocument
{
    [BsonId]
#pragma warning disable IDE1006 // Naming Styles
    public ObjectId _id { get; set; }
#pragma warning restore IDE1006 // Naming Styles

    public QueueStatus Status { get; set; }
    public EmailRequest? InputParameters { get; set; }
    public List<QueueStatusHistory> StatusHistory { get; set; } = [];
    public DateTime CreatedDate { get; set; }
    public bool DebugMode { get; set; }
    public string MachineName { get; set; } = default!;
    public int MaxRetryCount { get; set; }
    public int RetryCount { get; set; }
    public QueueType Type { get; set; }
    public QueuePriority Priority { get; set; }
}
