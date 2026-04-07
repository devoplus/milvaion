namespace Devoplus.JobForge.Entities;

public class Portal : MongoDBCollectionBase
{
    public string PortalName { get; set; } = default!;
    public string? DEXATunnelUrl { get; set; }
    public bool? DEXATunnelStatus { get; set; }
    public bool? IsDevEnvironment { get; set; }
}
