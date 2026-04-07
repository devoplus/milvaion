using MongoDB.Bson;

namespace Devoplus.JobForge.Entities.Queue;

public class EmailRequest
{
    public List<string>? ToAddresses { get; set; }
    public List<string>? CcAddresses { get; set; }
    public List<string>? BccAddresses { get; set; }
    public string Subject { get; set; } = default!;
    public string Body { get; set; } = default!;
    public ObjectId SenderPortalId { get; set; }
}
