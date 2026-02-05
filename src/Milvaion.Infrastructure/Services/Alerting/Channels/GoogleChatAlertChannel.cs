using Microsoft.Extensions.Options;
using Milvaion.Application.Dtos.AlertingDtos;
using Milvaion.Application.Dtos.NotificationDtos.GoogleChat;
using Milvasoft.Core.Abstractions;
using Milvasoft.Core.Utils.Constants;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Milvaion.Infrastructure.Services.Alerting.Channels;

/// <summary>
/// Alert channel implementation for Google Chat.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GoogleChatAlertChannel"/> class.
/// </remarks>
public class GoogleChatAlertChannel(IOptions<AlertingOptions> alertingOptions,
                                    IHttpClientFactory httpClientFactory,
                                    Lazy<IMilvaLogger> logger) : AlertChannelBase<GoogleChatChannelOptions>(alertingOptions)
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient(nameof(GoogleChatAlertChannel));
    private readonly Lazy<IMilvaLogger> _logger = logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    /// <inheritdoc/>
    public override string ChannelName => nameof(AlertChannelType.GoogleChat);

    /// <inheritdoc/>
    protected override GoogleChatChannelOptions GetChannelOptions(AlertingOptions options)
        => options.Channels?.GoogleChat;

    /// <inheritdoc/>
    protected override async Task<ChannelResult> SendCoreAsync(AlertType alertType, AlertPayload payload, CancellationToken cancellationToken)
    {
        var spaceConfig = _channelOptions.Spaces?.FirstOrDefault(s => string.Equals(s.Space, _channelOptions.DefaultSpace, StringComparison.OrdinalIgnoreCase));

        if (spaceConfig == null || string.IsNullOrWhiteSpace(spaceConfig.WebhookUrl))
            return ChannelResult.Skipped(ChannelName, "No webhook URL configured for the default space");

        var cardMessage = CreateCardMessage(alertType, payload);

        var jsonPayload = JsonSerializer.Serialize(cardMessage, _jsonOptions);

        var content = new StringContent(jsonPayload, Encoding.UTF8, MimeTypeNames.ApplicationJson);

        // Add thread reply option if thread key is specified
        var webHookUrl = cardMessage.Thread != null
            ? $"{spaceConfig.WebhookUrl}&messageReplyOption=REPLY_MESSAGE_FALLBACK_TO_NEW_THREAD"
            : spaceConfig.WebhookUrl;

        var response = await _httpClient.PostAsync(webHookUrl, content, cancellationToken);

        if (response.IsSuccessStatusCode)
            return ChannelResult.Successful(ChannelName);

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        _logger.Value.Warning("Google Chat alert failed. Status: {StatusCode}, Response: {Response}", response.StatusCode, responseBody);

        return ChannelResult.Failed(ChannelName, $"HTTP {response.StatusCode}: {responseBody}");
    }

    private GoogleChatCardMessage CreateCardMessage(AlertType alertType, AlertPayload payload)
    {
        var severityEmoji = GetSeverityEmoji(payload.Severity);

        var widgets = new List<Widget>
        {
            new()
            {
                DecoratedText = new DecoratedText
                {
                    StartIcon = new Icon { MaterialIcon = new MaterialIcon { Name = "info" } },
                    Text = $"<b>Type:</b> {alertType}"
                }
            },
            new()
            {
                DecoratedText = new DecoratedText
                {
                    StartIcon = new Icon { MaterialIcon = new MaterialIcon { Name = "schedule" } },
                    Text = $"<b>Time:</b> {payload.Timestamp:yyyy-MM-dd HH:mm:ss} UTC"
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(payload.Message))
        {
            widgets.Add(new Widget
            {
                DecoratedText = new DecoratedText
                {
                    StartIcon = new Icon { MaterialIcon = new MaterialIcon { Name = "description" } },
                    Text = $"<b>Message:</b> {TruncateMessage(payload.Message, 500)}"
                }
            });
        }

        if (!string.IsNullOrWhiteSpace(payload.Source))
        {
            widgets.Add(new Widget
            {
                DecoratedText = new DecoratedText
                {
                    StartIcon = new Icon { MaterialIcon = new MaterialIcon { Name = "source" } },
                    Text = $"<b>Source:</b> {payload.Source}"
                }
            });
        }

        var sections = new List<Section>
        {
            new() { Widgets = widgets }
        };

        // Add additional data section if present
        if (payload.AdditionalData != null)
        {
            var additionalDataJson = JsonSerializer.Serialize(payload.AdditionalData, _jsonOptions);

            if (additionalDataJson.Length <= 1000)
            {
                sections.Add(new Section
                {
                    Header = "Additional Data",
                    Widgets =
                    [
                        new Widget
                        {
                            DecoratedText = new DecoratedText
                            {
                                Text = $"<code>{additionalDataJson}</code>"
                            }
                        }
                    ]
                });
            }
        }

        return new GoogleChatCardMessage
        {
            CardsV2 =
            [
                new CardContainer
                {
                    CardId = $"alert-{alertType}-{payload.Timestamp.Ticks}",
                    Card = new Card
                    {
                        Header = new CardHeader
                        {
                            Title = $"{severityEmoji} {payload.Title ?? alertType.ToString()}",
                            Subtitle = $"Severity: {payload.Severity}"
                        },
                        Sections = sections
                    }
                }
            ],
            Thread = !string.IsNullOrWhiteSpace(payload.ThreadKey) ? new GoogleChatThread { ThreadKey = payload.ThreadKey } : null
        };
    }

    private static string TruncateMessage(string message, int maxLength)
    {
        if (string.IsNullOrEmpty(message) || message.Length <= maxLength)
            return message;

        return message[..(maxLength - 3)] + "...";
    }
}
