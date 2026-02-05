using Microsoft.Extensions.Options;
using Milvaion.Application.Dtos.AlertingDtos;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Utils.Constants;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Milvaion.Infrastructure.Services.Alerting.Channels;

/// <summary>
/// Alert channel implementation for Slack using Incoming Webhooks.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="SlackAlertChannel"/> class.
/// </remarks>
public class SlackAlertChannel(IOptions<AlertingOptions> alertingOptions,
                               IHttpClientFactory httpClientFactory,
                               Lazy<IMilvaLogger> logger) : AlertChannelBase<SlackChannelOptions>(alertingOptions)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(nameof(SlackAlertChannel));
    private readonly Lazy<IMilvaLogger> _logger = logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    /// <inheritdoc/>
    public override string ChannelName => nameof(AlertChannelType.Slack);

    /// <inheritdoc/>
    protected override SlackChannelOptions GetChannelOptions(AlertingOptions options) => options.Channels?.Slack;

    /// <inheritdoc/>
    protected override async Task<ChannelResult> SendCoreAsync(AlertType alertType, AlertPayload payload, CancellationToken cancellationToken)
    {
        var channelConfig = _channelOptions.Channels?.FirstOrDefault(c =>
            string.Equals(c.Channel, _channelOptions.DefaultChannel, StringComparison.OrdinalIgnoreCase));

        if (channelConfig == null || string.IsNullOrWhiteSpace(channelConfig.WebhookUrl))
            return ChannelResult.Skipped(ChannelName, "No webhook URL configured for the default channel");

        var slackMessage = CreateSlackMessage(alertType, payload);

        var jsonPayload = JsonSerializer.Serialize(slackMessage, _jsonOptions);

        var content = new StringContent(jsonPayload, Encoding.UTF8, MimeTypeNames.ApplicationJson);

        var response = await _httpClient.PostAsync(channelConfig.WebhookUrl, content, cancellationToken);

        if (response.IsSuccessStatusCode)
            return ChannelResult.Successful(ChannelName);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.Value.Warning("Slack alert failed. Status: {StatusCode}, Response: {Response}", response.StatusCode, responseBody);

        return ChannelResult.Failed(ChannelName, $"HTTP {response.StatusCode}: {responseBody}");
    }

    private SlackMessage CreateSlackMessage(AlertType alertType, AlertPayload payload)
    {
        var severityEmoji = GetSeverityEmoji(payload.Severity);
        var severityColor = GetSeverityColor(payload.Severity);

        var fields = new List<SlackField>
        {
            new() { Title = "Alert Type", Value = alertType.ToString(), Short = true },
            new() { Title = "Severity", Value = payload.Severity.ToString(), Short = true },
            new() { Title = "Time", Value = payload.Timestamp.ToString("yyyy-MM-dd HH:mm:ss") + " UTC", Short = true }
        };

        if (!string.IsNullOrWhiteSpace(payload.Source))
        {
            fields.Add(new SlackField { Title = "Source", Value = payload.Source, Short = true });
        }

        if (!string.IsNullOrWhiteSpace(payload.Message))
        {
            fields.Add(new SlackField { Title = "Message", Value = TruncateMessage(payload.Message, 500), Short = false });
        }

        if (payload.AdditionalData != null)
        {
            var additionalDataJson = JsonSerializer.Serialize(payload.AdditionalData, _jsonOptions);

            if (additionalDataJson.Length <= 500)
            {
                fields.Add(new SlackField { Title = "Additional Data", Value = $"```{additionalDataJson}```", Short = false });
            }
        }

        return new SlackMessage
        {
            Text = $"{severityEmoji} *{payload.Title ?? alertType.ToString()}*",
            Attachments =
            [
                new SlackAttachment
                {
                    Color = severityColor,
                    Fields = fields,
                    Footer = "Milvaion Alerting System",
                    Ts = new DateTimeOffset(payload.Timestamp).ToUnixTimeSeconds().ToString()
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

#region Slack Message Models

internal class SlackMessage
{
    [JsonPropertyName("text")]
    public string Text { get; set; }

    [JsonPropertyName("attachments")]
    public List<SlackAttachment> Attachments { get; set; }
}

internal class SlackAttachment
{
    [JsonPropertyName("color")]
    public string Color { get; set; }

    [JsonPropertyName("fields")]
    public List<SlackField> Fields { get; set; }

    [JsonPropertyName("footer")]
    public string Footer { get; set; }

    [JsonPropertyName("ts")]
    public string Ts { get; set; }
}

internal class SlackField
{
    [JsonPropertyName("title")]
    public string Title { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; }

    [JsonPropertyName("short")]
    public bool Short { get; set; }
}

#endregion
