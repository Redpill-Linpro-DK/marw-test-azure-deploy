namespace DIH.Common.Services.Messaging
{
    /// <summary>
    /// Provide simple messaging services.
    /// </summary>
    public interface IMessagingService : IAsyncDisposable
    {
        /// <summary>
        /// Send a message to a queue (single receiver) or topic (broadcast)
        /// </summary>
        /// <typeparam name="T">Type of message</typeparam>
        /// <param name="queueOrTopicName">Name of queue/topic</param>
        /// <param name="message">Message to send - must serializeable as JSON</param>
        /// <param name="compact">When false serialized JSON will be human readable, but use more bytes. Use compact for large messages.</param>
        /// <param name="label">Optional label to be set on service bus message. When null, no label will be set</param>
        Task EnqueueMessageAsync<T>(string queueOrTopicName, T message, bool compact = false, DateTimeOffset? scheduledEnqueueTime = null, string? label = null) where T : IMessage;
    }
}


