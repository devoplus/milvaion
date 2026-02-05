namespace Milvaion.Application.Dtos.AlertingDtos;

/// <summary>
/// Represents the result of sending an alert through one or more channels.
/// </summary>
public class AlertResult
{
    /// <summary>
    /// Gets or sets whether the overall alert operation was successful.
    /// True if at least one channel succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the individual channel results.
    /// </summary>
    public List<ChannelResult> ChannelResults { get; set; } = [];

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static AlertResult Successful(List<ChannelResult> channelResults = null) => new()
    {
        Success = true,
        ChannelResults = channelResults ?? []
    };

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static AlertResult Failed(List<ChannelResult> channelResults = null) => new()
    {
        Success = false,
        ChannelResults = channelResults ?? []
    };

    /// <summary>
    /// Creates a skipped result when alert is disabled.
    /// </summary>
    public static AlertResult Skipped(string reason) => new()
    {
        Success = true,
        ChannelResults = [new ChannelResult { ChannelName = "N/A", Success = true, Message = reason }]
    };
}

/// <summary>
/// Represents the result of sending an alert through a specific channel.
/// </summary>
public class ChannelResult
{
    /// <summary>
    /// Gets or sets the name of the channel.
    /// </summary>
    public string ChannelName { get; set; }

    /// <summary>
    /// Gets or sets whether the channel operation was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets an optional message describing the result.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets the exception if the operation failed.
    /// </summary>
    public Exception Exception { get; set; }

    /// <summary>
    /// Creates a successful channel result.
    /// </summary>
    public static ChannelResult Successful(string channelName, string message = null) => new()
    {
        ChannelName = channelName,
        Success = true,
        Message = message
    };

    /// <summary>
    /// Creates a failed channel result.
    /// </summary>
    public static ChannelResult Failed(string channelName, string message, Exception exception = null) => new()
    {
        ChannelName = channelName,
        Success = false,
        Message = message,
        Exception = exception
    };

    /// <summary>
    /// Creates a skipped channel result.
    /// </summary>
    public static ChannelResult Skipped(string channelName, string reason) => new()
    {
        ChannelName = channelName,
        Success = true,
        Message = $"Skipped: {reason}"
    };
}
