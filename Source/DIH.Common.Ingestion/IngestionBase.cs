using DIH.Common.Json;
using DIH.Common.Services.Messaging;
using DIH.Common.Services.Storage;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DIH.Common.Ingestion;
public abstract class IngestionBase
{
    protected DihLogger<IngestionBase> Logger { get; private set; }
    protected IStorageServiceDictionary StorageServiceDictionary { get; private set; }
    protected IStorageService StorageService { get; private set; }
    protected IMessagingService QueueService { get; private set; }
    protected IConfiguration Configuration { get; private set; }

    protected string StorageEndpoint { get; private set; }
    protected string AppDirectory { get; private set; }
    protected string IngestionChangeTopicName { get; private set; }
    protected string IngestionFullbatchTopicName { get; private set; }

    protected IngestionBase(
        ILogger<IngestionBase> logger,
        IConfiguration configuration,
        IOptions<ExecutionContextOptions> executionContextOptions,
        IStorageServiceDictionary storageServiceDictionary,
        IMessagingServiceDictionary queueServiceDictionary)
    {
        Logger = logger.AsDihLogger();
        Configuration = configuration;
        StorageServiceDictionary = storageServiceDictionary;
        StorageService = storageServiceDictionary.Get(Layer.Ingestion);
        QueueService = queueServiceDictionary.Get(Layer.Ingestion);
        AppDirectory = executionContextOptions.Value.AppDirectory;
        StorageEndpoint = Configuration[ConfigKeys.Ingestion_StorageHttpEndpoint] ?? throw new InvalidOperationException($"{ConfigKeys.Ingestion_StorageHttpEndpoint} config not set");
        IngestionFullbatchTopicName = configuration[ConfigKeys.Ingestion_Fullbatch_TopicName];
        IngestionChangeTopicName = configuration[ConfigKeys.Ingestion_Change_TopicName];
    }

    /// <summary>
    /// Ingests a large JSON document representing a data change by splitting it into smaller segments (chunks) and processing them individually.
    /// This method reads the entire JSON payload from the provided stream, calculates the total number of objects, segments the payload if necessary, and processes each segment asynchronously.
    /// </summary>
    /// <param name="dataStream">The input stream containing the full JSON document representing the data change. The stream is saved to a temporary file to allow for multiple reads during processing.</param>
    /// <param name="dataObjectTypeName">The name of the data object type being ingested (e.g., "Inventory"). This is used to determine storage and messaging settings.</param>
    /// <param name="changeType">The type of change being ingested (e.g., AddOrUpdate, Delete). This determines how the data change is processed.</param>
    /// <param name="batchId">A unique identifier for the batch job.</param>
    /// <param name="maxSegmentSize">The maximum number of objects allowed per segment. The document will be split into chunks of this size. Defaults to 1000 objects per segment.</param>
    /// <param name="appDirRelativeSchemaPath">Optional path to a schema file used to validate the JSON structure of each segment during ingestion. If provided, each segment is validated against this schema.</param>
    /// <returns>The total number of segments created and processed.</returns>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Saves the content of the provided data stream to a temporary file, allowing for multiple reads during processing.
    /// 2. Counts the total number of objects in the JSON array, which determines how many segments will be created.
    /// 3. Calculates the total number of segments based on the total object count and the `maxSegmentSize`.
    /// 4. Defines a segment handler that processes each segment by calling the `IngestChange` method.
    /// 5. Processes the JSON document in chunks, with each chunk containing up to `maxSegmentSize` objects. Each chunk is ingested individually.
    /// 6. Deletes the temporary file after all segments have been processed to clean up resources.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if any required configuration settings are missing or if there is an error during processing.</exception>
    /// <example>
    /// Suppose you have a JSON array containing 5000 objects, and the `maxSegmentSize` is set to 1000.
    /// This method will split the array into 5 segments, each containing 1000 objects, and process each segment separately.
    /// </example>
    public async Task<int> ProcessChange(Stream dataStream, string dataObjectTypeName, DataChangeType changeType, string batchId, int maxSegmentSize = 1000, string? appDirRelativeSchemaPath = null)
    {
        string tempFilePath = Path.GetTempFileName();

        try
        {
            using (var tempFileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await dataStream.CopyToAsync(tempFileStream);
            }

            // Calc total number of objects
            int totalObjects = 0;
            using (var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                totalObjects = await CountObjectsInArrayAsync(fileStream);
            }

            // Calculate the total number of segments
            int batchSegmentsTotal = (int)Math.Ceiling((double)totalObjects / maxSegmentSize);
            // Define the segment handler
            Func<Stream, int, Task> segmentHandler = (stream, batchSegment) =>
                ProcessPresegmentedChange(stream, dataObjectTypeName, changeType, batchId, batchSegment, appDirRelativeSchemaPath);

            using (var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await ProcessObjectsInChunksAsync(fileStream, maxSegmentSize, segmentHandler);
            }
            return batchSegmentsTotal;
        }
        finally
        {
            if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
        }
    }

    /// <summary>
    /// Ingests a single segment of a data change job by uploading the provided JSON document to Azure Blob Storage and queuing it for further processing.
    /// This method handles the ingestion of both validated and non-validated payloads, depending on whether a schema path is provided.
    /// </summary>
    /// <param name="dataStream">The input stream containing the JSON document representing the data change. This stream represents one segment of a data change job.</param>
    /// <param name="dataObjectTypeName">The name of the data object type being ingested (e.g., "Inventory"). This is used to determine the appropriate storage container and messaging settings.</param>
    /// <param name="changeType">The type of change being ingested (e.g., Create, Update, Delete). This determines how the data change is processed.</param>
    /// <param name="batchId">A unique identifier for the batch job. This ID is shared across all segments of the batch, ensuring continuity during processing.</param>
    /// <param name="appDirRelativeSchemaPath">Optional path to a JSON schema file used to validate the JSON document before uploading. If provided, the document will be validated against this schema. If null, no validation occurs.</param>
    /// <returns>A task representing the asynchronous ingestion operation.</returns>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Determines the storage container and generates a unique storage path for the JSON document in Azure Blob Storage.
    /// 2. If a schema path is provided, validates the JSON document against the schema during the upload process. If the schema validation fails, an exception is thrown, and the document is moved to a "SchemaError" location.
    /// 3. If no schema path is provided, uploads the JSON document directly to Blob Storage without validation.
    /// 4. Sends a message to the Service Bus topic to trigger downstream processing of the uploaded document.
    /// 5. Logs both the successful ingestion and any errors that occur during processing. If an error occurs, it is rethrown for further handling.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if required configuration settings are missing or if the JSON schema validation fails.</exception>
    /// <exception cref="InvalidDataException">Thrown if the JSON document fails schema validation or contains invalid data.</exception>
    /// <example>
    /// Suppose you have a JSON document representing a data change for the "Inventory" data object type. 
    /// This method will handle the ingestion of that change by uploading the JSON to Blob Storage, validating it if necessary, and queuing it for further processing.
    /// </example>
    public async Task ProcessPresegmentedChange(Stream dataStream, string dataObjectTypeName, DataChangeType changeType, string batchId, int batchSegment, string? appDirRelativeSchemaPath = null)
    {
        Logger.DihDebug($"Event Trigger Function for {dataObjectTypeName} ingestion (change) received a request", batchId);

        try
        {
            string containerName = Configuration[ConfigKeys.IngestionStorageContainerName(dataObjectTypeName)] ?? throw new InvalidOperationException($"{ConfigKeys.IngestionStorageContainerName(dataObjectTypeName)} config not set");

            string storagePath = $"IngestPost/{Regex.Replace(batchId, "[\\\\/:*?\"<>|]", "_")}_{batchSegment}.json";
            IngestionChangeMessage batchFileMessage = new IngestionChangeMessage(dataObjectTypeName, changeType, Layer.Ingestion, containerName, storagePath, batchId, segment: batchSegment);

            if (appDirRelativeSchemaPath != null)
            {
                // Upload the Blob while validating it
                Logger.DihInformation($"Upload validated {dataObjectTypeName} file to storage, {batchFileMessage.DataPayload}", batchFileMessage.BatchId);
                string schema = File.ReadAllText(Path.Combine(AppDirectory, appDirRelativeSchemaPath));
                await UploadValidatedPayload(dataStream, schema, batchFileMessage);
            }
            else
            {
                // Upload the Blob directly (no validation)
                Logger.DihInformation($"Upload {dataObjectTypeName} file to storage, {batchFileMessage.DataPayload}", batchFileMessage.BatchId);
                await batchFileMessage.UploadPayloadStreamAsync(StorageServiceDictionary, dataStream);
            }

            // Enqueue 
            Logger.DihInformation($"Enqueue message to topic (for raw and service listeners), batch id {batchFileMessage.BatchId} ({batchFileMessage.DataPayload})...");
            await QueueService.EnqueueMessageAsync(IngestionChangeTopicName, batchFileMessage, label: dataObjectTypeName);

            Logger.DihDebug(JsonConvert.SerializeObject(batchFileMessage, Formatting.Indented), batchFileMessage.BatchId);
        }
        catch (Exception ex)
        {
            if (ex is InvalidDataException)
            {
                Logger.DihWarning($"Bad '{dataObjectTypeName}' change ingest detected: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 500))}", batchId);
            }
            else
            {
                Logger.DihError(ex, batchId);
            }
            throw;
        }
    }

    /// <summary>
    /// Ingests a large JSON document as a segmented full batch job by splitting the document into smaller chunks (segments) and processing them individually.
    /// This method reads the entire JSON payload from the provided stream, calculates the total number of objects, segments the payload if necessary, and processes each segment asynchronously.
    /// </summary>
    /// <param name="dataStream">The input stream containing the full JSON document. The stream is saved to a temporary file to allow for multiple reads during processing.</param>
    /// <param name="dataObjectTypeName">The name of the data object type being ingested (e.g., "Inventory"). Used to determine storage and messaging settings.</param>
    /// <param name="batchId">A unique identifier for the batch job. This ID is shared across all segments of the batch, ensuring continuity.</param>
    /// <param name="maxSegmentSize">The maximum number of objects allowed per segment. The document will be split into chunks of this size. Defaults to 1000 objects per segment.</param>
    /// <param name="appDirRelativeSchemaPath">Optional path to a schema file used to validate the JSON structure of each segment during ingestion. If provided, each segment is validated against this schema.</param>
    /// <returns>The total number of segments created and processed.</returns>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Saves the content of the provided data stream to a temporary file, allowing for multiple reads during processing.
    /// 2. Counts the total number of objects in the JSON array, which determines how many segments will be created.
    /// 3. Calculates the total number of segments based on the total object count and the `maxSegmentSize`.
    /// 4. Defines a segment handler that processes each segment by calling the `IngestFullbatch` method.
    /// 5. Processes the JSON document in chunks, with each chunk containing up to `maxSegmentSize` objects. Each chunk is ingested individually.
    /// 6. Deletes the temporary file after all segments have been processed to clean up resources.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if any required configuration settings are missing or if there is an error during processing.</exception>
    /// <example>
    /// Suppose you have a JSON array containing 3000 objects, and the `maxSegmentSize` is set to 1000.
    /// This method will split the array into 3 segments, each containing 1000 objects, and process each segment separately.
    /// </example>
    public async Task<int> ProcessFullbatch(Stream dataStream, string dataObjectTypeName, string batchId, int maxSegmentSize = 1000, string? appDirRelativeSchemaPath = null)
    {
        string tempFilePath = Path.GetTempFileName();

        try
        {
            using (var tempFileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await dataStream.CopyToAsync(tempFileStream);
            }

            // Calc total number of objects
            int totalObjects = 0;
            using (var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                totalObjects = await CountObjectsInArrayAsync(fileStream);
            }

            // Calculate the total number of segments
            int batchSegmentsTotal = (int)Math.Ceiling((double)totalObjects / maxSegmentSize);
            // Define the segment handler
            Func<Stream, int, Task> segmentHandler = (stream, batchSegment) =>
                ProcessPresegmentedFullbatch(stream, dataObjectTypeName, batchId, batchSegment, batchSegmentsTotal, appDirRelativeSchemaPath);

            using (var fileStream = new FileStream(tempFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                await ProcessObjectsInChunksAsync(fileStream, maxSegmentSize, segmentHandler);
            }
            return batchSegmentsTotal;
        }
        finally
        {
            if (File.Exists(tempFilePath)) File.Delete(tempFilePath);
        }
    }


    /// <summary>
    /// Ingests a single segment of a full batch job by uploading the provided JSON document to Azure Blob Storage and queuing it for further processing.
    /// This method handles the ingestion of both validated and non-validated payloads, depending on whether a schema path is provided.
    /// </summary>
    /// <param name="dataStream">The input stream containing the JSON document to be ingested. This stream represents one segment of a full batch job.</param>
    /// <param name="dataObjectTypeName">The name of the data object type being ingested (e.g., "Inventory"). This is used to determine the appropriate storage container and messaging settings.</param>
    /// <param name="batchId">A unique identifier for the batch job. This ID is shared across all segments of the batch to ensure continuity during processing.</param>
    /// <param name="batchSegment">The current segment number of the JSON document. This indicates the position of the segment within the overall batch (e.g., segment 1 of 3).</param>
    /// <param name="batchSegmentsTotal">The total number of segments in the batch. This helps track the batch's progress and completion status.</param>
    /// <param name="appDirRelativeSchemaPath">Optional path to a JSON schema file used to validate the JSON document before uploading. If provided, the document will be validated against this schema. If null, no validation occurs.</param>
    /// <returns>A task representing the asynchronous ingestion operation.</returns>
    /// <remarks>
    /// This method performs the following steps:
    /// 1. Determines the storage container and generates a unique storage path for the JSON document in Azure Blob Storage.
    /// 2. If a schema path is provided, validates the JSON document against the schema during the upload process. If the schema validation fails, an exception is thrown, and the document is moved to a "SchemaError" location.
    /// 3. If no schema path is provided, uploads the JSON document directly to Blob Storage without validation.
    /// 4. Sets metadata on the uploaded blob to assist with debugging and tracking during further processing.
    /// 5. Sends a message to the Service Bus topic to trigger downstream processing of the uploaded document.
    /// 6. Logs both the successful ingestion and any errors that occur during processing. If an error occurs, it is rethrown for further handling.
    /// </remarks>
    /// <exception cref="InvalidOperationException">Thrown if required configuration settings are missing or if the JSON schema validation fails.</exception>
    /// <exception cref="InvalidDataException">Thrown if the JSON document fails schema validation or contains invalid data.</exception>
    /// <example>
    /// Suppose you have a batch job with 3 segments, each containing a JSON document. This method will handle the ingestion of each segment individually, uploading the JSON to Blob Storage and queuing it for further processing.
    /// </example>

    public async Task ProcessPresegmentedFullbatch(Stream dataStream, string dataObjectTypeName, string batchId, int batchSegment, int batchSegmentsTotal, string? appDirRelativeSchemaPath = null)
    {
        try
        {
            string containerName = Configuration[ConfigKeys.IngestionStorageContainerName(dataObjectTypeName)] ?? throw new InvalidOperationException($"{ConfigKeys.IngestionStorageContainerName(dataObjectTypeName)} config not set");

            string batchIdFilePart = Regex.Replace(batchId, "[\\\\/:*?\"<>|]", "_");
            string storagePath = $"IngestPost/{batchIdFilePart}_{batchSegmentsTotal}_{batchSegment}.json";
            IngestionFullBatchFileMessage batchFileMessage = new IngestionFullBatchFileMessage(dataObjectTypeName, Layer.Ingestion, containerName, storagePath, batchId, batchSegment, batchSegmentsTotal, ContentTypes.Json);

            if (appDirRelativeSchemaPath != null)
            {
                // Upload the Blob while validating it
                Logger.DihInformation($"Upload validated {dataObjectTypeName} file to storage, {batchFileMessage.StoragePath}", batchFileMessage.BatchId);
                string schema = File.ReadAllText(Path.Combine(AppDirectory, appDirRelativeSchemaPath));
                await UploadValidatedPayload(dataStream, schema, batchFileMessage);
            }
            else
            {
                // Upload the Blob directly (no validation)
                Logger.DihInformation($"Upload {dataObjectTypeName} file to storage, {batchFileMessage.StoragePath}", batchFileMessage.BatchId);
                await StorageService.UploadAsync(batchFileMessage.StorageContainerName, batchFileMessage.StoragePath, dataStream, batchFileMessage.ContentType);
            }

            // Set meta data ob blob file for yummy debug dev life
            await StorageService.SetMetadataAsync(batchFileMessage.StorageContainerName, batchFileMessage.StoragePath, batchFileMessage);

            // Enqueue 
            Logger.DihInformation($"Enqueue message to topic {IngestionFullbatchTopicName} (for raw and service listeners), batch id {batchFileMessage.BatchId} ");
            await QueueService.EnqueueMessageAsync(IngestionFullbatchTopicName, batchFileMessage, label: dataObjectTypeName);

            Logger.DihDebug(JsonConvert.SerializeObject(batchFileMessage, Formatting.Indented), batchFileMessage.BatchId);
        }
        catch (Exception ex)
        {
            if (ex is InvalidDataException)
            {
                Logger.DihWarning($"Bad '{dataObjectTypeName}' change ingest detected: {ex.Message.Substring(0, Math.Min(ex.Message.Length, 500))}", batchId);
            }
            else
            {
                Logger.DihError(ex, batchId);
            }
            throw;
        }
    }

    public async Task UploadValidatedPayload(Stream dataStream, string schema, IngestionFullBatchFileMessage batchFileMessage)
    {
        using (SchemaValidatingStream validatingStream = new(dataStream, schema))
        {
            try
            {
                // Upload text to storage.
                await StorageService.UploadAsync(batchFileMessage.StorageContainerName, batchFileMessage.StoragePath, validatingStream, batchFileMessage.ContentType);
                if (validatingStream.SchemaErrors.Any())
                {
                    throw new InvalidDataException($"Schema validation failed:\n - {string.Join("\n - ", validatingStream.SchemaErrors)}");
                }
            }
            catch (InvalidDataException)
            {
                string filename = batchFileMessage.StoragePath.Split('/').Last();
                string badJsonPath = $"{batchFileMessage.StoragePath.Replace(filename, "")}SchemaError/{DateTime.Now.ToString("yyyy/MM/dd")}/{filename}";
                await StorageService.MoveAsync(batchFileMessage.StorageContainerName, batchFileMessage.StoragePath, badJsonPath, deleteOld: true);

                throw;
            }
        }
    }

    public async Task UploadValidatedPayload(Stream dataStream, string schema, IngestionChangeMessage batchFileMessage)
    {
        using (SchemaValidatingStream validatingStream = new(dataStream, schema))
        {
            // Upload text to storage.
            await batchFileMessage.UploadPayloadStreamAsync(StorageServiceDictionary, validatingStream);
            if (validatingStream.SchemaErrors.Any())
            {
                await batchFileMessage.CleanupPayload(StorageServiceDictionary, "SchemaError");
                throw new InvalidDataException($"Schema validation failed:\n - {string.Join("\n - ", validatingStream.SchemaErrors)}");
            }
        }
    }


    private async Task<int> CountObjectsInArrayAsync(Stream dataStream)
    {
        int count = 0;

        using (var jsonDoc = await JsonDocument.ParseAsync(dataStream))
        {
            foreach (var property in jsonDoc.RootElement.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.Array)
                {
                    count = property.Value.GetArrayLength();
                    break;
                }
            }
        }

        return count;
    }

    private async Task ProcessObjectsInChunksAsync(Stream dataStream, int maxSegmentSize, Func<Stream, int, Task> segmentStreamHandler)
    {
        int currentBatchSegment = 1;
        int objectCount = 0;
        var jsonWriterOptions = new JsonWriterOptions { Indented = false };

        using (var streamReader = new StreamReader(dataStream))
        using (var jsonReader = new JsonTextReader(streamReader))
        {
            // Read through the JSON structure using JsonTextReader
            while (await jsonReader.ReadAsync())
            {
                if (jsonReader.TokenType == JsonToken.PropertyName)
                {
                    string propertyName = jsonReader.Value?.ToString() ?? "rootPropertyName";

                    // Look for the root property containing the array
                    if (await jsonReader.ReadAsync() && jsonReader.TokenType == JsonToken.StartArray)
                    {
                        await jsonReader.ReadAsync();
                        // Process the array elements in chunks
                        while (maxSegmentSize >= currentBatchSegment && jsonReader.TokenType != JsonToken.EndArray && jsonReader.Depth > 0)
                        {
                            using (var memoryStream = new MemoryStream())
                            using (var jsonWriter = new Utf8JsonWriter(memoryStream, jsonWriterOptions))
                            {
                                // Initialize the JSON structure
                                jsonWriter.WriteStartObject();
                                jsonWriter.WritePropertyName(propertyName);
                                jsonWriter.WriteStartArray();
                                // if (currentBatchSegment > 1) jsonWriter.WriteStartObject();

                                // Write the current chunk of elements
                                for (int i = 0; i < maxSegmentSize && jsonReader.TokenType != JsonToken.EndArray; i++, objectCount++)
                                {
                                    jsonWriter.WriteRawValue(await ReadCurrentJsonObjectAsync(jsonReader));
                                    if (!await jsonReader.ReadAsync())
                                        break;
                                }

                                // Close the JSON structure
                                jsonWriter.WriteEndArray();
                                jsonWriter.WriteEndObject();
                                await jsonWriter.FlushAsync();

                                // Reset the memory stream position for reading
                                memoryStream.Position = 0;

                                // Call the method with the current batch
                                await segmentStreamHandler(memoryStream, currentBatchSegment);

                                // Increment batch segment counter
                                currentBatchSegment++;
                            }
                        }
                    }
                }
            }
        }
    }

    // Helper function to read the current JSON object as a string
    private async Task<string> ReadCurrentJsonObjectAsync(JsonTextReader jsonReader)
    {
        var sb = new StringBuilder();
        using (var sw = new StringWriter(sb))
        using (var writer = new JsonTextWriter(sw))
        {
            await writer.WriteTokenAsync(jsonReader, true);
        }
        return sb.ToString();
    }
}
