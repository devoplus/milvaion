using Microsoft.Extensions.Options;
using Milvaion.Application.Dtos.AlertingDtos;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Utils.Constants;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Milvaion.Infrastructure.Services.Alerting.Channels;

/// <summary>
/// Alert channel implementation for Microsoft Teams using Incoming Webhooks with Adaptive Cards.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TeamsAlertChannel"/> class.
/// </remarks>
public class TeamsAlertChannel(IOptions<AlertingOptions> alertingOptions,
                               IHttpClientFactory httpClientFactory,
                               Lazy<IMilvaLogger> logger) : AlertChannelBase<TeamsChannelOptions>(alertingOptions)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(nameof(TeamsAlertChannel));
    private readonly Lazy<IMilvaLogger> _logger = logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc/>
    public override string ChannelName => nameof(AlertChannelType.Teams);

    /// <inheritdoc/>
    protected override TeamsChannelOptions GetChannelOptions(AlertingOptions options) => options.Channels?.Teams;

    /// <inheritdoc/>
    protected override async Task<ChannelResult> SendCoreAsync(AlertType alertType, AlertPayload payload, CancellationToken cancellationToken)
    {
        var channelConfig = _channelOptions.Channels?.FirstOrDefault(c =>
            string.Equals(c.Channel, _channelOptions.DefaultChannel, StringComparison.OrdinalIgnoreCase));

        if (channelConfig == null || string.IsNullOrWhiteSpace(channelConfig.WebhookUrl))
            return ChannelResult.Skipped(ChannelName, "No webhook URL configured for the default channel");

        var teamsMessage = CreateAdaptiveCardMessage(alertType, payload);

        var jsonPayload = JsonSerializer.Serialize(teamsMessage, _jsonOptions);

        var content = new StringContent(jsonPayload, Encoding.UTF8, MimeTypeNames.ApplicationJson);

        var response = await _httpClient.PostAsync(channelConfig.WebhookUrl, content, cancellationToken);

        if (response.IsSuccessStatusCode)
            return ChannelResult.Successful(ChannelName);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.Value.Warning("Teams alert failed. Status: {StatusCode}, Response: {Response}", response.StatusCode, responseBody);

        return ChannelResult.Failed(ChannelName, $"HTTP {response.StatusCode}: {responseBody}");
    }

    private TeamsWebhookMessage CreateAdaptiveCardMessage(AlertType alertType, AlertPayload payload)
    {
        var severityEmoji = GetSeverityEmoji(payload.Severity);

        var bodyItems = new List<AdaptiveElement>
        {
            new AdaptiveColumnSet
            {
                Columns =
                [
                    new AdaptiveColumn
                    {
                        Width = "auto",
                        Items =
                        [
                            new AdaptiveTextBlock
                            {
                                Text = severityEmoji,
                                Size = "Large"
                            }
                        ]
                    },
                    new AdaptiveColumn
                    {
                        Width = "stretch",
                        Items =
                        [
                            new AdaptiveTextBlock
                            {
                                Text = payload.Title ?? alertType.ToString(),
                                Size = "Large",
                                Weight = "Bolder",
                                Wrap = true
                            },
                            new AdaptiveTextBlock
                            {
                                Text = $"Severity: {payload.Severity}",
                                IsSubtle = true,
                                Spacing = "None"
                            }
                        ]
                    }
                ]
            },
            new AdaptiveFactSet
            {
                Facts =
                [
                    new AdaptiveFact { Title = "Alert Type", Value = alertType.ToString() },
                    new AdaptiveFact { Title = "Severity", Value = payload.Severity.ToString() },
                    new AdaptiveFact { Title = "Time", Value = $"{payload.Timestamp:yyyy-MM-dd HH:mm:ss} UTC" }
                ]
            }
        };

        if (!string.IsNullOrWhiteSpace(payload.Source))
        {
            ((AdaptiveFactSet)bodyItems[1]).Facts.Add(new AdaptiveFact { Title = "Source", Value = payload.Source });
        }

        if (!string.IsNullOrWhiteSpace(payload.Message))
        {
            bodyItems.Add(new AdaptiveTextBlock
            {
                Text = TruncateMessage(payload.Message, 500),
                Wrap = true
            });
        }

        if (payload.AdditionalData != null)
        {
            var additionalDataJson = JsonSerializer.Serialize(payload.AdditionalData, _jsonOptions);

            if (additionalDataJson.Length <= 500)
            {
                bodyItems.Add(new AdaptiveTextBlock
                {
                    Text = $"```\n{additionalDataJson}\n```",
                    Wrap = true,
                    FontType = "Monospace"
                });
            }
        }

        var card = new AdaptiveCard
        {
            Body = bodyItems,
            MsTeams = new MsTeamsProperties { Width = "Full" }
        };

        return new TeamsWebhookMessage
        {
            Attachments =
            [
                new TeamsAttachment
                {
                    Content = card
                }
            ]
        };
    }

    private static string TruncateMessage(string message, int maxLength)
    {
        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
            return message;

        return message[..(maxLength - 3)] + "...";
    }
}

#region Teams Adaptive Card Models

#pragma warning disable CA1822 // Mark members as static
internal class TeamsWebhookMessage
{
    [JsonPropertyName("type")]
    public string Type => "message";

    [JsonPropertyName("attachments")]
    public List<TeamsAttachment> Attachments { get; set; }
}

internal class TeamsAttachment
{
    [JsonPropertyName("contentType")]
    public string ContentType => "application/vnd.microsoft.card.adaptive";

    [JsonPropertyName("contentUrl")]
    public string ContentUrl => null;

    [JsonPropertyName("content")]
    public AdaptiveCard Content { get; set; }
}

internal class AdaptiveCard
{
    [JsonPropertyName("$schema")]
    public string Schema => "http://adaptivecards.io/schemas/adaptive-card.json";

    [JsonPropertyName("type")]
    public string Type => "AdaptiveCard";

    [JsonPropertyName("version")]
    public string Version => "1.4";

    [JsonPropertyName("body")]
    public List<AdaptiveElement> Body { get; set; }

    [JsonPropertyName("msteams")]
    public MsTeamsProperties MsTeams { get; set; }
}

internal class MsTeamsProperties
{
    [JsonPropertyName("width")]
    public string Width { get; set; }
}

[JsonDerivedType(typeof(AdaptiveTextBlock))]
[JsonDerivedType(typeof(AdaptiveFactSet))]
[JsonDerivedType(typeof(AdaptiveColumnSet))]
internal abstract class AdaptiveElement
{
    [JsonPropertyName("type")]
    public abstract string Type { get; }
}

internal class AdaptiveTextBlock : AdaptiveElement
{
    [JsonPropertyName("type")]
    public override string Type => "TextBlock";

    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("size")]
    public string Size { get; set; }

    [JsonPropertyName("weight")]
    public string Weight { get; set; }

    [JsonPropertyName("wrap")]
    public bool? Wrap { get; set; }

    [JsonPropertyName("isSubtle")]
    public bool? IsSubtle { get; set; }

    [JsonPropertyName("spacing")]
    public string Spacing { get; set; }

    [JsonPropertyName("fontType")]
    public string FontType { get; set; }
}

internal class AdaptiveFactSet : AdaptiveElement
{
    [JsonPropertyName("type")]
    public override string Type => "FactSet";

    [JsonPropertyName("facts")]
    public List<AdaptiveFact> Facts { get; set; }
}

internal class AdaptiveFact
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; }
}

internal class AdaptiveColumnSet : AdaptiveElement
{
    [JsonPropertyName("type")]
    public override string Type => "ColumnSet";

    [JsonPropertyName("columns")]
    public List<AdaptiveColumn> Columns { get; set; }
}

internal class AdaptiveColumn
{
    [JsonPropertyName("type")]
    public string Type => "Column";

    [JsonPropertyName("width")]
    public string Width { get; set; }

    [JsonPropertyName("items")]
    public List<AdaptiveElement> Items { get; set; }
}
#pragma warning restore CA1822 // Mark members as static
#endregion
