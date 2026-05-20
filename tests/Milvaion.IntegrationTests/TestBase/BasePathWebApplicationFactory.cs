namespace Milvaion.IntegrationTests.TestBase;

/// <summary>
/// Factory that configures the application with BasePath = "/milvaion".
/// Used for verifying that all API endpoints, SignalR hubs, static files and SPA
/// fallback work correctly when the application is hosted under a sub-path.
/// </summary>
public class BasePathWebApplicationFactory : CustomWebApplicationFactory
{
    protected override string DatabaseName => "testDb_basepath";

    protected override void ConfigureAdditionalEnvironmentVariables()
        => Environment.SetEnvironmentVariable("MilvaionConfig__BasePath", "/milvaion");
}
