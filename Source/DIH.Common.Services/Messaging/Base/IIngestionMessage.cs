namespace DIH.Common.Services.Messaging.Base
{
    /// <summary>
    /// Base interface for messages
    /// </summary>
    public interface IIngestionMessage : IMessage
    {
        /// <summary>
        /// The data type this message relate to
        /// </summary>
        string DataObjectTypeName { get; set; }

        /// <summary>
        /// Unique id that identify the batch of this message
        /// </summary>
        string BatchId { get; }

        /// <summary>
        /// Descriptive key that identify the batch of this message (DataObjectTypeName + BatchId)
        /// </summary>
        string DihKey { get; }

        /// <summary>
        /// When this message was created
        /// </summary>
        DateTime Created { get; set; }
    }
}

