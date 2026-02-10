namespace Milvaion.IntegrationTests.TestBase;

/// <summary>
/// Test collection for Services and BackgroundServices tests.
/// Uses an isolated database to run in parallel with the Controllers collection.
/// </summary>
[CollectionDefinition(nameof(ServicesTestCollection))]
public class ServicesTestCollection : ICollectionFixture<ServicesWebApplicationFactory>;
