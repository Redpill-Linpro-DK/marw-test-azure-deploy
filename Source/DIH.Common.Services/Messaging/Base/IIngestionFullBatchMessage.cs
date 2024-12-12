namespace DIH.Common.Services.Messaging.Base
{
    /// <summary>
    /// Represents a message that is part of a batch job.
    /// </summary>
    interface IIngestionFullBatchMessage : IIngestionMessage
    {
        /// <summary>
        /// The total number of messages that this message is part of
        /// </summary>
        int BatchSegmentsTotal { get; set; }

        /// <summary>
        /// The position number of this message. Between 1 and BatchSegmentsTotal
        /// </summary>
        int BatchSegment { get; set; }

        /// <summary>
        /// True if the task of this message is considered completed
        /// </summary>
        bool Completed { get; set; }
    }
}


