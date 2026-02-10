namespace Milvaion.IntegrationTests.TestBase;

/// <summary>
/// Factory for Services &amp; BackgroundServices test collection.
/// Uses an isolated database "testDb_services" to enable parallel execution
/// with the Controllers test collection.
/// </summary>
public class ServicesWebApplicationFactory : CustomWebApplicationFactory
{
    protected override string DatabaseName => "testDb_services";
}
