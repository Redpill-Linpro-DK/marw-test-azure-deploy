using DIH.Common.Services.Messaging.Base;
using DIH.Common.Services.Storage;
using System.Text;

namespace DIH.Common.Services.Messaging
{
    public class IngestionChangeMessage : IIngestionMessage
    {
        public IngestionChangeMessage()
        {
            Created = DateTime.Now;
            BatchId = $"{Guid.NewGuid()}";
            DataObjectTypeName = "";
            DataPayload = "";
            Segment = 1;
        }


        /// <summary>
        /// Construct a inline JSON RawDataChangeMessage
        /// </summary>
        /// <param name="DataObjectTypeName"></param>
        /// <param name="ChangeType"></param>
        /// <param name="json"></param>
        public IngestionChangeMessage(string dataObjectTypeName, DataChangeType changeType, string json, string? batchId = null, int segment = 1)
        {
            DataObjectTypeName = dataObjectTypeName;
            ChangeType = changeType;
            DataPayload = json;
            DataPayloadType = DataPayloadType.Inline;
            Created = DateTime.Now;
            BatchId = batchId ?? $"{Guid.NewGuid()}";
            Segment = segment;
        }

        /// <summary>
        /// Construct a storage based JSON RawDataChangeMessage
        /// </summary>
        /// <param name="DataObjectTypeName"></param>
        /// <param name="ChangeType"></param>
        /// <param name="json"></param>
        public IngestionChangeMessage(string dataObjectTypeName, DataChangeType changeType, Layer storageLayer, string storageContainerName, string storagePath, string? batchId = null, int segment = 1)
        {
            DataObjectTypeName = dataObjectTypeName;
            ChangeType = changeType;
            DataPayload = $"{storageLayer}:{storageContainerName}:{storagePath}";
            DataPayloadType = DataPayloadType.StorageIdentifier;
            Created = DateTime.Now;
            BatchId = batchId ?? $"{Guid.NewGuid()}";
            Segment = segment;
        }

        public string DataObjectTypeName { get; set; }
        public DataChangeType ChangeType { get; set; }
        public DataPayloadType DataPayloadType { get; set; }
        public string DataPayload { get; set; }
        public DateTime Created { get; set; }

        public string BatchId { get; set; }
        public int Segment { get; set; }
        public string DihKey
        {
            get { return $"{DataObjectTypeName}:{BatchId}:{Segment}"; }
        }


        public async Task UploadPayloadStreamAsync(IStorageServiceDictionary storageServiceDictionary, Stream payloadStream)
        {
            if (DataPayloadType == DataPayloadType.StorageIdentifier)
            {
                var storageIdentifierParts = DataPayload.Split(':');
                Layer sourceLayer = ((Layer)Enum.Parse(typeof(Layer), storageIdentifierParts[0]));
                string containerName = storageIdentifierParts[1];
                string path = string.Join(':', storageIdentifierParts.Skip(2));
                IStorageService storageService = storageServiceDictionary.Get(sourceLayer);
                await storageService.UploadAsync(containerName, path, payloadStream);
                await storageService.SetMetadataAsync(containerName, path, this);
            }
            else
            {
                throw new InvalidOperationException($"{nameof(IngestionChangeMessage)} was instanciated with payload stored inline - to upload a stream, instanciate as storage based");
            }
        }

        /// <summary>
        /// Helper that return Stream to the data (payload) of this message
        /// </summary>
        /// <param name="storageServiceDictionary">Service dictionary - when payload is storage based, this should contain the storage service for the layer holding the file</param>
        /// <returns>Stream to data</returns>
        public async Task<Stream> GetPayloadStream(IStorageServiceDictionary storageServiceDictionary)
        {
            switch (DataPayloadType)
            {
                case DataPayloadType.Inline:
                    MemoryStream stream = new MemoryStream();
                    StreamWriter writer = new StreamWriter(stream, Encoding.UTF8);
                    writer.Write(DataPayload);
                    writer.Flush();
                    stream.Position = 0;
                    return stream;
                case DataPayloadType.StorageIdentifier:
                    var storageIdentifierParts = DataPayload.Split(':');
                    Layer sourceLayer = ((Layer)Enum.Parse(typeof(Layer), storageIdentifierParts[0]));
                    string containerName = storageIdentifierParts[1];
                    string path = string.Join(':', storageIdentifierParts.Skip(2));
                    IStorageService storageService = storageServiceDictionary.Get(sourceLayer);
                    return await storageService.DownloadAsync(containerName, path);
                default:
                    throw new NotImplementedException($"Unknown DataPayloadType {DataPayloadType}");
            }
        }

        public async Task CleanupPayload(IStorageServiceDictionary storageServiceDictionary, string cleanUpFolder)
        {
            if (DataPayloadType == DataPayloadType.Inline) return;

            if (DataPayloadType != DataPayloadType.StorageIdentifier) throw new NotImplementedException("Unhandled DataPayloadType");

            var storageIdentifierParts = DataPayload.Split(':');
            Layer storageLayer = ((Layer)Enum.Parse(typeof(Layer), storageIdentifierParts[0]));
            string storageContainerName = storageIdentifierParts[1];
            string storagePath = string.Join(':', storageIdentifierParts.Skip(2));
            IStorageService storageService = storageServiceDictionary.Get(storageLayer);

            var filename = storagePath.Split('/').Last();
            var newPath = $"{storagePath.Replace(filename, "")}{cleanUpFolder}/{DateTime.Now.ToString("yyyy/MM/dd")}/{filename}";

            await storageService.MoveAsync(storageContainerName, storagePath, newPath, deleteOld: true);

            DataPayload = $"{storageLayer}:{storageContainerName}:{storagePath}";
        }

        public async Task DeletePayload(IStorageServiceDictionary storageServiceDictionary)
        {
            if (DataPayloadType == DataPayloadType.Inline) return;

            if (DataPayloadType != DataPayloadType.StorageIdentifier) throw new NotImplementedException("Unhandled DataPayloadType");

            var storageIdentifierParts = DataPayload.Split(':');
            Layer storageLayer = ((Layer)Enum.Parse(typeof(Layer), storageIdentifierParts[0]));
            string storageContainerName = storageIdentifierParts[1];
            string storagePath = string.Join(':', storageIdentifierParts.Skip(2));
            IStorageService storageService = storageServiceDictionary.Get(storageLayer);

            await storageService.DeleteAsync(storageContainerName, storagePath);
        }
    }
}