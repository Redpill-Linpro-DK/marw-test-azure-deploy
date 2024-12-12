namespace DIH.Common.Services.Messaging
{
    /// <summary>
    /// Contains IMessagingService instances to access message services of diffferent layers.
    /// </summary>
    public interface IMessagingServiceDictionary : IAsyncDisposable
    {
        /// <summary>
        /// Returna a IMessagingService instance to access files from the specified layer
        /// </summary>
        /// <param name="name">The layer owning the messages</param>
        /// <returns>Message service</returns>
        IMessagingService Get(Layer name);
    }
}


