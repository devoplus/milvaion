namespace Milvaion.IntegrationTests.TestBase;

/// <summary>
/// Test collection for Worker SDK tests.
/// Uses lightweight fixture with only RabbitMQ + Redis containers (no PostgreSQL/WAF).
/// Tests in this collection run in parallel with DB-dependent collections.
/// </summary>
[CollectionDefinition(nameof(WorkerSdkTestCollection))]
public class WorkerSdkTestCollection : ICollectionFixture<WorkerSdkContainerFixture>;
