using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Devoplus.JobForge.Entities;

public class UserGroup : MongoDBCollectionBase
{
    public string UserGroupName { get; set; } = default!;

    [BsonRepresentation(BsonType.ObjectId)]
    public List<ObjectId>? CompanyIds { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public List<ObjectId>? DepartmentIds { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public List<ObjectId>? EmployeeTypeIds { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public List<ObjectId>? TitleIds { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public List<ObjectId>? WorkplaceIds { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public List<ObjectId>? UserIds { get; set; }
}
