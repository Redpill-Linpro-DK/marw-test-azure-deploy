using DIH.Common.Services.Messaging.Base;

namespace DIH.Common.Services.Messaging
{
    public class IngestionFullBatchFileMessage : IngestionFullBatchMessage
    {
        public Layer StorageLayer { get; set; }
        public string StorageContainerName { get; set; }
        public string StoragePath { get; set; }
        public string ContentType { get; set; }

        /// <summary>
        /// Reflect a batch file
        /// </summary>
        /// <param name="dataObjectTypeName">The layer which storage account holds the batch file</param>
        /// <param name="storageLayer">The layer which storage account holds the batch file</param>
        /// <param name="storageContainerName">The name of the container that holds the batch file</param>
        /// <param name="storagePath">The local path of the batch file on the storage account / container identified by storageLayer and storageContainerName</param>
        /// <param name="batchId"></param>
        /// <param name="batchSegment">The segment number of this batch</param>
        /// <param name="batchSegmentsTotal">The total number of segments in this batch</param>
        /// <param name="contentType"></param>
        public IngestionFullBatchFileMessage(
            string dataObjectTypeName,
            Layer storageLayer,
            string storageContainerName,
            string storagePath,
            string batchId,
            int batchSegment,
            int batchSegmentsTotal,
            string contentType) : base(dataObjectTypeName, batchId, batchSegment, batchSegmentsTotal)
        {
            StorageLayer = storageLayer;
            StorageContainerName = storageContainerName;
            StoragePath = storagePath;
            ContentType = contentType;
        }

        // For framework deserialization
        public IngestionFullBatchFileMessage()
        {
            StorageLayer = Layer.Undefined;
            StorageContainerName = "";
            StoragePath = "";
            ContentType = "";
        }
    }
}

