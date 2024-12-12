using System.Text;

namespace DIH.Common
{
    /// <summary>
    /// Represents a layer in DIH
    /// </summary>
    public enum Layer
    {
        Undefined = -1,
        Ingestion = 0,
        DataRaw = 10,
        DataPrepared = 11,
        Service = 20
    }

    /// <summary>
    /// Represents the type of change that has happened to a data object
    /// </summary>
    public enum DataChangeType
    {
        Delete = -1,
        AddOrUpdate = 1,
        Patch = 2
    }

    /// <summary>
    /// Represents the location of data in a message. When Inline data is expected to be in the DataPayload field. When StorageIdentifier data is expected to be located in a Storage file, identified by the value of the DataPayload field.
    /// </summary>
    public enum DataPayloadType
    {
        Inline = 0,
        StorageIdentifier = 1,
    }


    /// <summary>
    /// Key names that are expected to exist in the local environment.
    /// On Azure as configuration on the Function App.
    /// On developer machines in local.settings.json
    /// </summary>
    public static class EnvironmentConfigKeys
    {
        // URL to Azure App Configuration service, ex. https://config-dihbaseline-dev-01.azconfig.io
        public const string AzureAppConfigurationEndpoint = "AzureAppConfigurationEndpoint";
        public const string LocalKeyVaultUri = "LocalKeyVault:Uri";
    }

    /// <summary>
    /// Key names that are expected to exist in applications configuration
    /// On Azure as configuration on the Azure App Configuration instance.
    /// On developer machines in local.settings.json (optional if they exist on the App Configuration instance).
    /// </summary>
    public static class ConfigKeys
    {
        // Sentinel Key - when value of this config key changes, apps will reload all configs (within 5 minutes) without requiring a restart
        public const string DIH_Config_SentinelKey = "DIH:Config:Sentinel";


        // Developer workarounds
        public const string DIH_Developer_OnlyUseStandardConnections = "DIH:Developer:OnlyUseStandardConnections";

        //KeyVault URL
        public const string DIH_GlobalKeyVault_Uri = "GlobalKeyVault:Uri";

        // DIH General behaviour - constraints functions should adhere to
        public const string DIH_Functions_MaxParallelTasks = "DIH:Functions:MaxParallelTasks";
        public const string DIH_Functions_ResourceIntensive_MaxParallelTasks = "DIH:Functions:ResourceIntensive:MaxParallelTasks";
        public const string DIH_Functions_ExternalWebApi_MaxParallelTasks = "DIH:Functions:ExternalWebApi:MaxParallelTasks";
        public const string DIH_Functions_MaxInMemObjects = "DIH:Functions:MaxInMemObjects";
        public const string DIH_Functions_MaxTasksPerMessage = "DIH:Functions:MaxTasksPerMessage";
        public const string DIH_Functions_BatchTimeoutSeconds = "DIH:Functions:BatchTimeoutSeconds";
        public const string DIH_Functions_MessageTTLSeconds = "DIH:Functions:MessageTTLSeconds";
        public const string DIH_Functions_CancelFullBatchOnException = "DIH:Functions:CancelFullBatchOnException";

        // Ingestion layer
        public const string Ingestion_StorageHttpEndpoint = "Ingestion:StorageHttpEndpoint";
        public const string Ingestion_ServiceBusHttpEndpoint = "Ingestion:Msg:ServiceBus:fullyQualifiedNamespace";
        public static string IngestionStorageContainerName(string dataObjectName) => $"Ingestion:{dataObjectName}:StorageContainerName";
        public static string IngestionChangeQueueName(string dataObjectName) => $"Ingestion:{dataObjectName}:Change:QueueName";
        public static string IngestionFullbatchQueueName(string dataObjectName) => $"Ingestion:{dataObjectName}:Fullbatch:QueueName";
        public const string Ingestion_Change_TopicName = "Ingestion:IngestionChange:TopicName";
        public const string Ingestion_Fullbatch_TopicName = "Ingestion:IngestionFullbatch:TopicName";
        public const string Ingestion_DataObjectTypeNames = "Ingestion:DataObjectTypeNames";

        //
        // Data Raw layer queues / tables
        //
        public const string Data_Raw_ServiceBusHttpEndpoint = "Data:Raw:ServiceBus:fullyQualifiedNamespace";
        public const string Data_Raw_ReceiveChange_SubscriptionName = "Data:Raw:ReceiveChange:SubscriptionName";
        public const string Data_Raw_ReceiveFullbatch_SubscriptionName = "Data:Raw:ReceiveFullbatch:SubscriptionName";
        public const string Data_Raw_ReceiveFullbatch_QueueName = "Data:Raw:ReceiveFullbatch:QueueName";
        public const string Data_Raw_ReceiveFullBatch_PurgePlan_QueueName = "Data:Raw:ReceiveFullBatchPurgePlan:QueueName";
        public const string Data_Raw_ReceiveFullBatch_PurgeExecute_QueueName = "Data:Raw:ReceiveFullBatchPurgeExecute:QueueName";
        public const string Data_Raw_ReceiveFullBatch_Broadcast_QueueName = "Data:Raw:ReceiveFullBatchBroadcast:QueueName";
        public const string Data_Raw_ReceiveFullBatch_Cleanup_QueueName = "Data:Raw:ReceiveFullBatchCleanup:QueueName";
        public const string Data_Raw_ReceiveFullBatch_Abort_QueueName = "Data:Raw:ReceiveFullBatchAbort:QueueName";
        public const string Data_Raw_TableBatchesHandled = "Data:Raw:Table:BatchesHandled";
        public const string Data_Raw_TableUnchangedIds = "Data:Raw:Table:UnchangedIds";
        public const string Data_Raw_TableDeletedIds = "Data:Raw:Table:DeletedIds";
        public const string Data_Raw_TableImportLogs = "Data:Raw:Table:ImportLog";
        public const string Data_Raw_TableActiveBatches = "Data:Raw:Table:ActiveBatches";
        public const string Data_Raw_TableCanceledBatches = "Data:Raw:Table:CanceledBatches";
        public const string Data_Raw_TableChangesHandled = "Data:Raw:Table:ChangesHandled";
        public const string Data_Raw_Change_TopicName = "Data:Raw:RawDataChange:TopicName";
        public const string Data_Raw_CosmosAccountEndpoint = "Data:Raw:CosmosAccountEndpoint";
        public const string Data_Raw_TableServiceEndpoint = "Data:Raw:TableServiceEndpoint";

        // Max % of existing raw data (after upserts) we allow to be deleted - first is default, second is for prefix of dataObjectTypeName for a type specific number
        public const string Data_Raw_MaxDeletePercent = "Data:Raw:MaxDeletePercent";
        public const string Data_Raw_MaxDeletePercent_PREFIX = "Data:Raw:MaxDeletePercent:";

        // bool - when true, documenta to be purged will be "soft deleted" (have __DIH_DELETED set to true)
        public const string Data_Raw_SoftDelete = "Data:Raw:SoftDelete";

        // int - days run history / handled batch files are kept around
        public const string Data_Raw_HistoryRetentionDays = "Data:Raw:HistoryRetentionDays";

        // int - days run history / handled batch files are kept around
        public const string Data_Raw_SoftDeletedRetentionDays = "Data:Raw:SoftDeletedRetentionDays";

        // ID substitution feature - create key named Data:Raw:IdSubstitute:(data obj name) to create rule
        public const string Data_Raw_IdSubstitute_PREFIX = "Data:Raw:IdSubstitute:";

        //
        // Data Prepared layer service endpoints
        //
        public const string Data_Prepared_ServiceBusHttpEndpoint = "Data:Prepared:ServiceBus:fullyQualifiedNamespace";
        public const string Data_Prepared_CosmosAccountEndpoint = "Data:Prepared:CosmosAccountEndpoint";
        public const string Data_Prepared_TableServiceEndpoint = "Data:Prepared:TableServiceEndpoint";
        public const string Data_Prepared_Table_DataLastRequest = "Data:Prepared:Table:DataLastRequest";
        public const string Data_Prepared_UnrequestedDataRetentionDays = "Data:Prepared:UnrequestedDataRetentionDays";
        public static string DataPreparedCopyFromRaw(string dataObjectName) => $"Data:Prepared:{dataObjectName}:CopyFromRaw";

        //
        // Data Prepared layer queues / tables
        //
        public const string Data_Prepared_Change_TopicName = "Data:Prepared:PreparedDataChange:TopicName";

        //
        // Services layer
        // 
        public const string Services_DefaultPageItemCount = "Services:DefaultPageItemCount";
        public const string Services_MaxPageItemCount = "Services:MaxPageItemCount";
        public const string Service_DataObjectTypeNames = "Service:DataObjectTypeNames";

        //
        // Service queues / tables
        //
        public const string Data_Service_Prepared_Data_Change_SubscriptionName = "Data:Service:PreparedDataChange:SubscriptionName";

        //
        // General data
        //
        public static string DataPartitionKey(Layer layer, string dataObjectTypeName)
        {
            StringBuilder sb = new("Data:");
            switch (layer)
            {
                case Layer.DataRaw:
                    sb.Append("Raw:");
                    break;
                case Layer.DataPrepared:
                    sb.Append("Prepared:");
                    break;
                default:
                    throw new NotImplementedException($"Unhandled layer {layer}");
            }
            sb.Append("PartitionKey:");
            sb.Append(dataObjectTypeName);
            return sb.ToString();
        }
    }

    /// <summary>
    /// HTTP Headers accepted by the Ingestion layer
    /// </summary>
    public static class HttpHeaders
    {
        public const string XBatchId = "X-Batch-Id";
        public const string XBatchSegmentsTotal = "X-Batch-Segments-Total";
        public const string XBatchSegment = "X-Batch-Segment";
        public const string ContentType = "Content-Type";
    }

    /// <summary>
    /// Content types recognized 
    /// </summary>
    public static class ContentTypes
    {
        public const string Unknown = "application/octet-stream";
        public const string Json = "application/json";
    }

    /// <summary>
    /// Core JSON property names
    /// </summary>
    public static class JsonPropertyNames
    {
        public const string Id = "id";
        public const string DIH_Hash = "__DIH_HASH";
        public const string DIH_LastUpdate = "__DIH_LAST_UPDATE";
        public const string DIH_UpdatingBatchId = "__DIH_UPDATING_BATCHID";
        public const string DIH_Status = "__DIH_Status";

        public static readonly string[] DIH_Properties = { JsonPropertyNames.DIH_Hash, JsonPropertyNames.DIH_LastUpdate, JsonPropertyNames.DIH_UpdatingBatchId, JsonPropertyNames.DIH_Status };
    }

    /// <summary>
    /// Core JSON property values / value appends
    /// </summary>
    public static class JsonPropertyValues
    {
        public const string DIH_AbortedHashAppendMark = "::ABORTED";
        public const string DIH_Status_SoftDeleted = "SOFT_DELETED";
        public const string DIH_Status_Active = "ACTIVE";
    }

    /// <summary>
    /// Names of databases
    /// </summary>
    public static class DatabaseNames
    {
        public const string Raw = "raw";
        public const string Prepared = "prepared";
    }
}

