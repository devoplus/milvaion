using MongoDB.Bson.Serialization.Attributes;

namespace Devoplus.JobForge.Entities.Nested;

public enum EmailAddressType
{
    Work = 0,
    Personal = 1,
    Other = 2
}

[BsonIgnoreExtraElements]
public class Email
{
    public EmailAddressType EmailAddressType { get; set; }
    public string? EmailAddress { get; set; }
}
