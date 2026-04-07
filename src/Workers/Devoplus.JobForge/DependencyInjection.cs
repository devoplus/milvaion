using Devoplus.JobForge.Core.Settings;
using Devoplus.JobForge.DataAccess;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Devoplus.JobForge;

public static class DependencyInjection
{
    public static IServiceCollection AddJobForgeInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var mongoSettings = configuration.GetSection(MongoDbSettings.SectionKey).Get<MongoDbSettings>()
            ?? throw new InvalidOperationException($"'{MongoDbSettings.SectionKey}' configuration section is missing or invalid.");

        services.AddSingleton(mongoSettings);
        services.AddSingleton<IMongoDbService, MongoDbService>();
        services.AddSingleton<IMailQueueService, MailQueueService>();
        services.AddHttpClient();

        return services;
    }
}
