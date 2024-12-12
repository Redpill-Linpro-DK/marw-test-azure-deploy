using DIH.Common.Services.Messaging;
using DIH.Common.Services.Storage;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace DIH.Common.Ingestion;
public abstract class AsyncIngestionBase : IngestionBase
{
    public AsyncIngestionBase(
        ILogger<AsyncIngestionBase> logger,
        IConfiguration configuration,
        IOptions<ExecutionContextOptions> executionContextOptions,
        IStorageServiceDictionary storageServiceDictionary,
        IMessagingServiceDictionary queueServiceDictionary) : base(logger, configuration, executionContextOptions, storageServiceDictionary, queueServiceDictionary)
    { }

    /// <summary>
    /// Ingests a stream as a change
    /// </summary>
    /// <param name="dataStream">Stream containing JSON document</param>
    /// <param name="dataObjectTypeName">Name of the data object type being changed</param>
    /// <param name="changeType">Type of change</param>
    /// <param name="batchId">ID of batch (optional)</param>
    /// <param name="appDirRelativeSchemaPath">Path to schema (optional). When null no schema validation happen</param>
    /// <returns></returns>
    public async Task QueueChange(Stream dataStream, string dataObjectTypeName, DataChangeType changeType, string? batchId = null, string? appDirRelativeSchemaPath = null)
    {
        string setBatchID = batchId ?? $"{Guid.NewGuid()}";

        try
        {
            string containerName = Configuration[ConfigKeys.IngestionStorageContainerName(dataObjectTypeName)] ?? throw new InvalidOperationException($"{ConfigKeys.IngestionStorageContainerName(dataObjectTypeName)} config not set");
            string queueName = Configuration[ConfigKeys.IngestionChangeQueueName(dataObjectTypeName)] ?? throw new InvalidOperationException($"{ConfigKeys.IngestionChangeQueueName(dataObjectTypeName)} config not set");

            string storagePath = $"IngestPost/{Regex.Replace(setBatchID, "[\\\\/:*?\"<>|]", "_")}_received.json";
            IngestionChangeMessage batchFileMessage = new IngestionChangeMessage(dataObjectTypeName, changeType, Layer.Ingestion, containerName, storagePath, setBatchID);

            if (appDirRelativeSchemaPath != null)
            {
                // Upload the Blob while validating it
                string schema = File.ReadAllText(Path.Combine(AppDirectory, appDirRelativeSchemaPath));
                await UploadValidatedPayload(dataStream, schema, batchFileMessage);
            }
            else
            {
                // Upload the Blob directly (no validation)
                await batchFileMessage.UploadPayloadStreamAsync(StorageServiceDictionary, dataStream);
            }

            // Enqueue 
            Logger.DihInformation($"Enqueue message to orchestrator, batch id {batchFileMessage.BatchId} ({batchFileMessage.DataPayload})...");
            await QueueService.EnqueueMessageAsync(queueName, batchFileMessage);
        }
        catch (Exception ex)
        {
            if (ex is InvalidDataException)
            {
                Logger.DihWarning($"Bad '{dataObjectTypeName}' change ingest detected: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 500))}", setBatchID);
            }
            else
            {
                Logger.DihError(ex, setBatchID);
            }
            throw;
        }
    }

    public async Task ProcessChangeFromMessage(string serviceBusMessageText, Func<Stream, Task<Stream>> payloadTransformer, int maxSegmentSize = 1000, string? appDirRelativeSchemaPath = null)
    {
        // Deserialize 
        IngestionChangeMessage? batchFileMessage = JsonConvert.DeserializeObject<IngestionChangeMessage>(serviceBusMessageText) ?? throw new InvalidOperationException($"Unable to deserialize message as {nameof(IngestionChangeMessage)}");
        string batchId = batchFileMessage.BatchId;
        string dataObjectTypeName = batchFileMessage.DataObjectTypeName;

        try
        {
            Stream originalDataStream = await batchFileMessage.GetPayloadStream(StorageServiceDictionary);

            Stream orchestratedDataStream = await payloadTransformer(originalDataStream);

            // Did original sender already segment data into multiple segments? If not, we ensure segmentation is done (with max 1000 objects per segment)
            int batchSegmentsTotal = await base.ProcessChange(orchestratedDataStream, dataObjectTypeName, batchFileMessage.ChangeType, batchId, maxSegmentSize: maxSegmentSize, appDirRelativeSchemaPath: appDirRelativeSchemaPath);

            string monitorMessage = $"{batchFileMessage.ChangeType} of '{dataObjectTypeName}' ({batchSegmentsTotal} segments total) orchestrated";
            Logger.DihMonitorSuccess(DihMonitorSystemType.DIH, DihMonitorSystemName.DihIngestionOrchestration, DihMonitorSystemType.DIH, DihMonitorSystemName.DihIngestionChange, batchId: batchId, dataObjectTypeName: dataObjectTypeName, message: monitorMessage);

            // Clean up original file - no longer needed
            await batchFileMessage.DeletePayload(StorageServiceDictionary);
        }
        catch (Exception ex)
        {
            Logger.DihMonitorError(DihMonitorSystemType.DIH, DihMonitorSystemName.DihIngestionOrchestration, DihMonitorSystemType.DIH, DihMonitorSystemName.DihIngestionChange, exception: ex, batchId: batchId, dataObjectTypeName: dataObjectTypeName);
            throw;
        }
    }

    /// <summary>
    /// Queues a full batch job for orchestration by uploading the provided data stream to Azure Blob Storage and sending a message to the Service Bus.
    /// </summary>
    /// <param name="dataStream">The stream containing the JSON document that represents a segment of the full batch.</param>
    /// <param name="dataObjectTypeName">The name of the data object type being ingested (e.g., "Inventory"). This is used to determine the appropriate storage container and queue.</param>
    /// <param name="batchId">A unique identifier for the batch job. This ID is shared across all segments of the same batch.</param>
    /// <param name="batchSegment">The current segment number of the JSON document. This indicates the position of the segment within the overall batch (e.g., segment 1 of 3).</param>
    /// <param name="batchSegmentsTotal">The total number of segments in the batch. This helps track the batch's progress and completion status.</param>
    /// <param name="appDirRelativeSchemaPath">Optional path to a JSON schema file used to validate the JSON document before uploading. If provided, the document will be validated against this schema.</param>
    /// <exception cref="InvalidOperationException">Thrown if required configuration settings are missing or if the data validation fails.</exception>
    /// <returns>A task representing the asynchronous operation of queuing the full batch for orchestration.</returns>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Determines the storage container and queue names based on the data object type.
    /// 2. Generates a sanitized storage path for the JSON document in Azure Blob Storage.
    /// 3. Uploads the JSON document to Blob Storage, optionally validating it against a provided schema.
    /// 4. Sets metadata on the uploaded blob to help track the batch segment and other details.
    /// 5. Sends a message to the Service Bus queue to trigger the orchestration of the batch job.
    /// If an error occurs during any of these steps, it is logged, and the exception is rethrown for further handling.
    /// </remarks>
    protected async Task QueueFullbatch(Stream dataStream, string dataObjectTypeName, string batchId, int batchSegment, int batchSegmentsTotal, string? appDirRelativeSchemaPath = null)
    {
        try
        {
            string containerName = Configuration[ConfigKeys.IngestionStorageContainerName(dataObjectTypeName)] ?? throw new InvalidOperationException($"{ConfigKeys.IngestionStorageContainerName(dataObjectTypeName)} config not set");
            string queueName = Configuration[ConfigKeys.IngestionFullbatchQueueName(dataObjectTypeName)] ?? throw new InvalidOperationException($"{ConfigKeys.IngestionFullbatchQueueName(dataObjectTypeName)} config not set");
            string batchIdFilePart = Regex.Replace(batchId, "[\\\\/:*?\"<>|]", "_");
            string storagePath = $"IngestPost/{batchIdFilePart}_received_{batchSegmentsTotal}_{batchSegment}.json";

            IngestionFullBatchFileMessage batchFileMessage = new IngestionFullBatchFileMessage(dataObjectTypeName, Layer.Ingestion, containerName, storagePath, batchId, batchSegment, batchSegmentsTotal, ContentTypes.Json);

            if (appDirRelativeSchemaPath != null)
            {
                // Upload the Blob while validating it
                Logger.DihInformation($"Upload validated {dataObjectTypeName} file to storage, {batchFileMessage.StorageContainerName}:{batchFileMessage.StoragePath}", batchFileMessage.BatchId);
                string schema = File.ReadAllText(Path.Combine(AppDirectory, appDirRelativeSchemaPath));
                await UploadValidatedPayload(dataStream, schema, batchFileMessage);
            }
            else
            {
                // Upload the Blob directly (no validation)
                Logger.DihInformation($"Upload {dataObjectTypeName} file to storage, {batchFileMessage.StorageContainerName}:{batchFileMessage.StoragePath}", batchFileMessage.BatchId);
                await StorageService.UploadAsync(batchFileMessage.StorageContainerName, batchFileMessage.StoragePath, dataStream, batchFileMessage.ContentType);
            }

            // Update Blob meta data.
            await StorageService.SetMetadataAsync(batchFileMessage.StorageContainerName, batchFileMessage.StoragePath, batchFileMessage);

            // Enqueue 
            Logger.DihInformation($"Enqueue message to orchestrator, batch id {batchFileMessage.BatchId} ({batchFileMessage.BatchSegment}/{batchFileMessage.BatchSegmentsTotal})...");
            await QueueService.EnqueueMessageAsync(queueName, batchFileMessage);

            Logger.DihDebug(JsonConvert.SerializeObject(batchFileMessage, Formatting.Indented), batchFileMessage.BatchId);
            Logger.DihDebug($"HTTP Trigger Function for {dataObjectTypeName} done", batchFileMessage.BatchId);
        }
        catch (Exception ex)
        {
            if (ex is InvalidDataException)
            {
                Logger.DihWarning($"Bad '{dataObjectTypeName}' fullbatch ingest detected: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 500))}", batchId);
            }
            else
            {
                Logger.DihError(ex, batchId);
            }
            throw;
        }
    }

    /// <summary>
    /// Orchestrates the payload by downloading the original data stream from Azure Blob Storage, transforming the payload if necessary, 
    /// and processing it as a full batch job or segmented batch job based on the original segmentation.
    /// </summary>
    /// <param name="serviceBusMessageText">The message text received from the Service Bus, containing metadata about the batch job, including storage details and segmentation information.</param>
    /// <param name="payloadTransformer">A function that takes the original data stream and returns a transformed data stream, allowing for custom processing of the payload before ingestion.</param>
    /// <param name="maxSegmentSize">The maximum number of objects allowed per segment if segmentation is required. Defaults to 1000 objects per segment.</param>
    /// <param name="appDirRelativeSchemaPath">Optional path to a schema file used for validating the JSON structure of the payload during ingestion. If provided, the payload is validated against this schema.</param>
    /// <returns>A task representing the asynchronous orchestration operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the Service Bus message cannot be deserialized or if an error occurs during processing.</exception>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Deserializes the Service Bus message to retrieve batch metadata, including storage container, path, and segmentation details.
    /// 2. Downloads the original JSON payload from Azure Blob Storage using the metadata.
    /// 3. Applies the provided payload transformation function to the original data stream.
    /// 4. Depending on the segmentation information:
    ///    - If the batch was already segmented (i.e., `BatchSegmentsTotal > 1`), processes the current segment as part of the full batch job.
    ///    - If the batch was not segmented (i.e., `BatchSegmentsTotal == 1`), segments the payload into smaller chunks (based on `maxSegmentSize`) and processes them individually.
    /// 5. Logs success or failure of the orchestration process, and cleans up the original file from Blob Storage once processing is complete.
    /// If an error occurs at any stage, it is logged for monitoring purposes and rethrown for further handling.
    /// </remarks>

    protected async Task ProcessFullbatchFromMessage(string serviceBusMessageText, Func<Stream, Task<Stream>> payloadTransformer, int maxSegmentSize = 1000, string? appDirRelativeSchemaPath = null)
    {
        // Deserialize 
        IngestionFullBatchFileMessage batchFileMessage = JsonConvert.DeserializeObject<IngestionFullBatchFileMessage>(serviceBusMessageText) ?? throw new InvalidOperationException($"Unable to deserialize message as {nameof(IngestionFullBatchFileMessage)}");
        string batchId = batchFileMessage.BatchId;
        string dataObjectTypeName = batchFileMessage.DataObjectTypeName;

        try
        {
            IStorageService storageService = StorageServiceDictionary.Get(batchFileMessage.StorageLayer);
            Stream originalDataStream = await storageService.DownloadAsync(batchFileMessage.StorageContainerName, batchFileMessage.StoragePath);

            Stream orchestratedDataStream = await payloadTransformer(originalDataStream);

            // Did original sender already segment data into multiple segments? If not, we ensure segmentation is done (with max 1000 objects per segment)
            if (batchFileMessage.BatchSegmentsTotal == 1)
            {
                int batchSegmentsTotal = await base.ProcessFullbatch(orchestratedDataStream, dataObjectTypeName, batchId, maxSegmentSize: maxSegmentSize, appDirRelativeSchemaPath: appDirRelativeSchemaPath);

                string monitorMessage = $"Full batch of '{dataObjectTypeName}' ({batchSegmentsTotal} segments total) orchestrated";
                Logger.DihMonitorSuccess(DihMonitorSystemType.DIH, DihMonitorSystemName.DihIngestionOrchestration, DihMonitorSystemType.DIH, DihMonitorSystemName.DihIngestionFullbatch, batchId: batchId, dataObjectTypeName: dataObjectTypeName, message: monitorMessage);
            }
            else
            {
                await base.ProcessPresegmentedFullbatch(orchestratedDataStream, dataObjectTypeName, batchId, batchFileMessage.BatchSegment, batchFileMessage.BatchSegmentsTotal, appDirRelativeSchemaPath: appDirRelativeSchemaPath);

                string monitorMessage = $"Full batch of '{dataObjectTypeName}' (segment {batchFileMessage.BatchSegment} of {batchFileMessage.BatchSegmentsTotal}) orchestrated";
                Logger.DihMonitorSuccess(DihMonitorSystemType.DIH, DihMonitorSystemName.DihIngestionOrchestration, DihMonitorSystemType.DIH, DihMonitorSystemName.DihIngestionFullbatch, batchId: batchId, dataObjectTypeName: dataObjectTypeName, message: monitorMessage);
            }

            // Clean up original file - no longer needed
            await storageService.DeleteAsync(batchFileMessage.StorageContainerName, batchFileMessage.StoragePath);
        }
        catch (Exception ex)
        {
            Logger.DihMonitorError(DihMonitorSystemType.DIH, DihMonitorSystemName.DihIngestionOrchestration, DihMonitorSystemType.DIH, DihMonitorSystemName.DihIngestionFullbatch, exception: ex, batchId: batchId, dataObjectTypeName: dataObjectTypeName);
            throw;
        }
    }
}
