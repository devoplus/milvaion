using Milvasoft.Core.Abstractions;
using RabbitMQ.Client;

namespace Milvaion.Infrastructure.Extensions;

/// <summary>
/// Extension methods for RabbitMQ IChannel operations.
/// Provides thread-safe ACK/NACK operations with proper error handling.
/// </summary>
public static class RabbitMQChannelExtensions
{
    /// <summary>
    /// Safe ACK operation that checks channel state before acknowledgment.
    /// Uses provided lock for thread-safety as IChannel is NOT thread-safe.
    /// Prevents "Already closed" exceptions during shutdown.
    /// </summary>
    /// <param name="channel">The RabbitMQ channel.</param>
    /// <param name="deliveryTag">The delivery tag to acknowledge.</param>
    /// <param name="channelLock">Semaphore for thread-safe channel access.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="multiple">If true, ACKs all messages up to and including deliveryTag.</param>
    public static async Task SafeAckAsync(this IChannel channel,
                                          ulong deliveryTag,
                                          SemaphoreSlim channelLock,
                                          IMilvaLogger logger,
                                          CancellationToken cancellationToken,
                                          bool multiple = false)
    {
        if (cancellationToken.IsCancellationRequested || channel == null || channel.IsClosed)
        {
            logger?.Debug("Skipping ACK: Channel closed or shutdown requested (DeliveryTag: {DeliveryTag})", deliveryTag);
            return;
        }

        await channelLock.WaitAsync(cancellationToken);

        try
        {
            if (channel == null || channel.IsClosed)
            {
                logger?.Debug("Skipping ACK: Channel closed after acquiring lock (DeliveryTag: {DeliveryTag})", deliveryTag);
                return;
            }

            await channel.BasicAckAsync(deliveryTag, multiple, cancellationToken);
        }
        catch (RabbitMQ.Client.Exceptions.AlreadyClosedException)
        {
            logger?.Debug("Channel already closed during ACK (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown - don't log as warning
            logger?.Debug("ACK cancelled (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
        catch (Exception ex)
        {
            logger?.Warning(ex, "Failed to ACK message (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
        finally
        {
            channelLock.Release();
        }
    }

    /// <summary>
    /// Safe NACK operation that checks channel state before negative acknowledgment.
    /// Uses provided lock for thread-safety as IChannel is NOT thread-safe.
    /// Prevents "Already closed" exceptions during shutdown.
    /// </summary>
    /// <param name="channel">The RabbitMQ channel.</param>
    /// <param name="deliveryTag">The delivery tag to negative acknowledge.</param>
    /// <param name="channelLock">Semaphore for thread-safe channel access.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <param name="multiple">If true, NACKs all messages up to and including deliveryTag.</param>
    /// <param name="requeue">If true, requeues the message(s).</param>
    public static async Task SafeNackAsync(this IChannel channel,
                                           ulong deliveryTag,
                                           SemaphoreSlim channelLock,
                                           IMilvaLogger logger,
                                           CancellationToken cancellationToken,
                                           bool multiple = false,
                                           bool requeue = false)
    {
        if (cancellationToken.IsCancellationRequested || channel == null || channel.IsClosed)
        {
            logger?.Debug("Skipping NACK: Channel closed or shutdown requested (DeliveryTag: {DeliveryTag})", deliveryTag);
            return;
        }

        await channelLock.WaitAsync(cancellationToken);

        try
        {
            if (channel == null || channel.IsClosed)
            {
                logger?.Debug("Skipping NACK: Channel closed after acquiring lock (DeliveryTag: {DeliveryTag})", deliveryTag);
                return;
            }

            await channel.BasicNackAsync(deliveryTag, multiple, requeue, cancellationToken);
        }
        catch (RabbitMQ.Client.Exceptions.AlreadyClosedException)
        {
            logger?.Debug("Channel already closed during NACK (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown - don't log as warning
            logger?.Debug("NACK cancelled (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
        catch (Exception ex)
        {
            logger?.Warning(ex, "Failed to NACK message (DeliveryTag: {DeliveryTag})", deliveryTag);
        }
        finally
        {
            channelLock.Release();
        }
    }

    /// <summary>
    /// Safely closes and disposes the channel with proper error handling.
    /// </summary>
    /// <param name="channel">The RabbitMQ channel.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SafeCloseAsync(this IChannel channel,
                                            IMilvaLogger logger,
                                            CancellationToken cancellationToken)
    {
        if (channel == null)
            return;

        try
        {
            if (!channel.IsClosed)
            {
                await channel.CloseAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger?.Warning(ex, "Error closing RabbitMQ channel");
        }
        finally
        {
            channel.Dispose();
        }
    }

    /// <summary>
    /// Safely closes and disposes the connection with proper error handling.
    /// </summary>
    /// <param name="connection">The RabbitMQ connection.</param>
    /// <param name="logger">Logger for diagnostics.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SafeCloseAsync(this IConnection connection,
                                            IMilvaLogger logger,
                                            CancellationToken cancellationToken)
    {
        if (connection == null)
            return;

        try
        {
            if (connection.IsOpen)
            {
                await connection.CloseAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger?.Warning(ex, "Error closing RabbitMQ connection");
        }
        finally
        {
            connection.Dispose();
        }
    }
}
