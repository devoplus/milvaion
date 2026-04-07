using Devoplus.JobForge.DataAccess;
using Devoplus.JobForge.Entities;
using Devoplus.JobForge.Entities.Nested;
using Devoplus.JobForge.Entities.Queue;
using Milvasoft.Milvaion.Sdk.Worker.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Net;

namespace Devoplus.JobForge.Jobs.Workflow;

public class HealthCheck : IAsyncJob
{
    private readonly IMongoDbService _mongoDbService;
    private readonly IMailQueueService _mailQueueService;

    public HealthCheck(IMongoDbService mongoDbService, IMailQueueService mailQueueService)
    {
        _mongoDbService = mongoDbService;
        _mailQueueService = mailQueueService;
    }

    public async Task ExecuteAsync(IJobContext context)
    {
        context.LogInformation("🚀 HealthCheck started!");

        var portalCollection = _mongoDbService.GetCollection<Portal>();
        var userCollection = _mongoDbService.GetCollection<User>();
        var userGroupCollection = _mongoDbService.GetCollection<UserGroup>();

        var portals = portalCollection
            .Find(t => t.IsActive && (t.IsDevEnvironment == false || t.IsDevEnvironment == null))
            .ToList();

        foreach (var portal in portals)
        {
            var systemUser = userCollection
                .Find(t => t.PortalId == portal.PortalId && t.IsCompanyAdministrator && t.UserCode == "system" && t.IsActive)
                .FirstOrDefault();

            // Resolve health-check notification group members' work e-mails
            var healthCheckGroupId = ObjectId.Parse("6703889fee761c7ee8ff3a34");
            var healthCheckGroup = userGroupCollection.Find(t => t._id == healthCheckGroupId).FirstOrDefault();
            var healthCheckMails = new List<string>();

            if (healthCheckGroup?.UserIds?.Count > 0)
            {
                var groupUsers = userCollection.Find(t => healthCheckGroup.UserIds.Contains(t._id)).ToList();
                foreach (var user in groupUsers)
                {
                    foreach (var mail in user.EmailAddresses)
                    {
                        if (mail.EmailAddressType == EmailAddressType.Work && mail.EmailAddress != null)
                            healthCheckMails.Add(mail.EmailAddress);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(portal.DEXATunnelUrl))
            {
                context.LogWarning($"DEXA Tunnel Url not found. PortalId: {portal._id}");
                continue;
            }

            //var systemMailAddress = "burak.unsal@suvari.com.tr";
            var systemMailAddress = "burak.besli@suvari.com.tr";

            if (systemUser?.EmailAddresses != null)
            {
                foreach (var mail in systemUser.EmailAddresses)
                {
                    if (mail.EmailAddressType == EmailAddressType.Work && mail.EmailAddress != null)
                    {
                        systemMailAddress = mail.EmailAddress;
                        break;
                    }
                }
            }
            else
            {
                context.LogWarning($"{portal.PortalName} system user not found. PortalId: {portal._id}");
            }

            var pingUrl = portal.DEXATunnelUrl + "Ping";
            var controlDateTime = DateTime.UtcNow;

#pragma warning disable SYSLIB0014
            var request = WebRequest.Create(pingUrl);
#pragma warning restore SYSLIB0014

            try
            {
                using var response = request.GetResponse();
                var httpResponse = (HttpWebResponse)response;
                context.LogInformation($"Request succeeded with status code: {httpResponse.StatusCode}, PortalId: {portal._id}");
                portalCollection.UpdateOne(
                    Builders<Portal>.Filter.Eq(x => x._id, portal._id),
                    Builders<Portal>.Update.Set(y => y.DEXATunnelStatus, true));
            }
            catch (WebException e)
            {
                if (e.Response is HttpWebResponse errorResponse)
                {
                    context.LogError($"Request failed with status code: {errorResponse.StatusCode}, PortalId: {portal._id}");

                    if (portal.DEXATunnelStatus != false)
                    {
                        await _mailQueueService.EnqueueAsync(new EmailRequest
                        {
                            ToAddresses = [systemMailAddress],
                            CcAddresses = healthCheckMails,
                            Subject = $"{portal.PortalName} HealthCheck",
                            Body = $"{portal.PortalName} DEXATunnel is unreachable. Status: {errorResponse.StatusCode} (UTC: {controlDateTime})",
                            SenderPortalId = ObjectId.Empty
                        });
                    }

                    portalCollection.UpdateOne(
                        Builders<Portal>.Filter.Eq(x => x._id, portal._id),
                        Builders<Portal>.Update.Set(y => y.DEXATunnelStatus, false));
                }
                else
                {
                    context.LogError($"Request failed: {e.Message}, PortalId: {portal._id}");
                }
            }
        }

        context.LogInformation("✅ HealthCheck completed successfully!");
    }
}

