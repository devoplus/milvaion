using Devoplus.JobForge.Entities.Nested;

namespace Devoplus.JobForge.Entities;

public class User : MongoDBCollectionBase
{
    public string UserCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public bool IsCompanyAdministrator { get; set; }
    public List<Email> EmailAddresses { get; set; } = [];
}
