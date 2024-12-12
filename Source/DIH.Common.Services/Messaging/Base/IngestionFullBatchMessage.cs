namespace DIH.Common.Services.Messaging.Base
{
    public class IngestionFullBatchMessage : IIngestionFullBatchMessage
    {
        public string DataObjectTypeName { get; set; }
        public string BatchId { get; set; }
        public int BatchSegmentsTotal { get; set; }
        public int BatchSegment { get; set; }
        public bool Completed { get; set; }
        public DateTime? CompletedTime { get; set; }
        public DateTime Created { get; set; }

        public string DihKey
        {
            get
            {
                return $"{DataObjectTypeName}:{BatchId}";
            }
        }

        /// <summary>
        /// Reflect a batch file
        /// </summary>
        /// <param name="dataObjectTypeName">The layer which storage account holds the batch file</param>
        /// <param name="batchId"></param>
        /// <param name="batchSegment"></param>
        /// <param name="batchSegmentsTotal"></param>
        public IngestionFullBatchMessage(
            string dataObjectTypeName,
            string batchId,
            int batchSegment,
            int batchSegmentsTotal
            )
        {
            DataObjectTypeName = dataObjectTypeName;
            BatchId = batchId;
            BatchSegmentsTotal = batchSegmentsTotal;
            BatchSegment = batchSegment;
            Created = DateTime.Now;
        }

        public IngestionFullBatchMessage()
        {
            DataObjectTypeName = "";
            BatchId = "";
            BatchSegmentsTotal = 1;
            BatchSegment = 1;
            Created = DateTime.Now;
        }
    }
}

