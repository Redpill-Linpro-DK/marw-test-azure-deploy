using DIH.Common.Services.Database;

namespace DIH.Common.Services.Messaging
{
    public class DataChangeMessage : IMessage
    {
        public string DataObjectTypeName { get; set; }
        public Layer SourceLayer { get; set; }
        public List<DataObjectIdentifier> ChangedDataObjectIdentifiers { get; set; }
        public DataChangeType ChangeType { get; set; }
        public string BatchId { get; set; }

        /// <summary>
        /// Reflect a change in raw data - should be consumed by Data Prepared 
        /// </summary>
        /// <param name="dataObjectTypeName">The layer which storage account holds the batch file</param>
        /// <param name="storageLayer">The layer which storage account holds the batch file</param>
        /// <param name="changedDataObjectIdentifiers">The name of the container that holds the batch file</param>
        /// <param name="changeType">The local path of the batch file on the storage account / container identified by storageLayer and storageContainerName</param>
        /// <param name="batchId"></param>
        public DataChangeMessage(
            string dataObjectTypeName,
            Layer storageLayer,
            IEnumerable<DataObjectIdentifier> changedDataObjectIdentifiers,
            DataChangeType changeType,
            string batchId)
        {
            DataObjectTypeName = dataObjectTypeName;
            SourceLayer = storageLayer;
            ChangedDataObjectIdentifiers = changedDataObjectIdentifiers.ToList();
            ChangeType = changeType;
            BatchId = batchId;
        }

        /// <summary>
        /// Required for deserialization
        /// </summary>
        public DataChangeMessage()
        {
            DataObjectTypeName = "";
            BatchId = "";
            ChangedDataObjectIdentifiers = new List<DataObjectIdentifier>();
        }
    }
}


