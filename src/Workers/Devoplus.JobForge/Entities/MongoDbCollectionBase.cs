using Devoplus.JobForge.Core.Utilities;
using Devoplus.JobForge.Entities.Nested;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace Devoplus.JobForge.Entities;

/// <summary>
/// MongoDB'de yer alan koleksiyonların tamamında yer alacak alanların bulunduğu baz sınıf
/// </summary>
[BsonIgnoreExtraElements(Inherited = true)]
public abstract class MongoDBCollectionBase
{
    /// <summary>
    /// MongoDB ObjectId
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonConverter(typeof(ObjectIdConverter))]
#pragma warning disable IDE1006 // Naming Styles
    public ObjectId _id { get; set; } = ObjectId.GenerateNewId();
#pragma warning restore IDE1006 // Naming Styles
    /// <summary>
    /// Nesnenin farklı bir nesneyle replace edilmesi halinde yeni nesnenin ObjectId bilgisi
    /// </summary>
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonConverter(typeof(ObjectIdConverter))]
    public ObjectId? ReplacementId { get; set; }
    /// <summary>
    /// Nesnenin vekili varsa vekil olan ObjectId bilgisi
    /// </summary>
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonConverter(typeof(ObjectIdConverter))]
    public ObjectId? DelegatedId { get; set; }
    /// <summary>
    /// Mevcut nesnenin hangi portal'a ait olduğunu tanımlayan Id bilgisi
    /// </summary>
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonConverter(typeof(ObjectIdConverter))]
    public ObjectId? PortalId { get; set; }
    /// <summary>
    /// Mevcut nesnenin meta verileri
    /// </summary>
    public List<Metadata> Metadata { get; set; } = new List<Metadata>();
    /// <summary>
    /// Mevcut nesnenin entegrasyonlar ile senkronize edilip edilmeyeceği bilgisi
    /// </summary>
    public bool SyncWithIntegration { get; set; } = true;
    /// <summary>
    /// Mevcut nesnenin aktif olup olmadığı bilgisi
    /// </summary>
    public bool IsActive { get; set; }
    /// <summary>
    /// Nesnenin oluşturulma tarihi
    /// </summary>
    public DateTime CreatedDate { get; set; }
    /// <summary>
    /// Nesnenin güncelleme tarihi
    /// </summary>
    public DateTime UpdatedDate { get; set; }
    /// <summary>
    /// Nesneyi oluşturan kişi
    /// </summary>
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonConverter(typeof(ObjectIdConverter))]
    public ObjectId CreatedBy { get; set; } = ObjectId.Empty;
    /// <summary>
    /// Nesneyi güncelleyen kişi
    /// </summary>
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonConverter(typeof(ObjectIdConverter))]
    public ObjectId UpdatedBy { get; set; } = ObjectId.Empty;
}