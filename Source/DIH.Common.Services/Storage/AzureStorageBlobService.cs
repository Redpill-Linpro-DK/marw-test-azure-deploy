using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DIH.Common.Credential;
using Microsoft.Extensions.Logging;

namespace DIH.Common.Services.Storage
{
    public class AzureStorageBlobService : IStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly DihLogger<AzureStorageBlobService> Logger;

        public AzureStorageBlobService(ILogger<AzureStorageBlobService> logger, string blobServiceEndpoint)
        {
            if (string.IsNullOrEmpty(blobServiceEndpoint)) throw new ArgumentNullException(nameof(blobServiceEndpoint));

            Logger = logger.AsDihLogger();

            _blobServiceClient = new BlobServiceClient(new Uri(blobServiceEndpoint), AzureCredentialFactory.BuildDefault());
        }

        public async Task UploadAsync(string containerName, string path, Stream stream, string contentType)
        {
            Logger.DihDebug("UploadAsync");
            ValidateContainerAndPath(containerName, path);
            ArgumentNullException.ThrowIfNull(stream, nameof(stream));
            if (string.IsNullOrEmpty(contentType)) throw new ArgumentNullException(nameof(contentType));

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(path);

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            };

            await blobClient.UploadAsync(stream, options);
        }

        public async Task MoveAsync(string containerName, string oldPath, string newPath, bool deleteOld = true)
        {
            Logger.DihDebug("MoveAsync");
            ValidateContainerAndPath(containerName, oldPath);
            ValidateContainerAndPath(containerName, newPath);

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var sourceBlobClient = containerClient.GetBlobClient(oldPath);
            var destinationBlobClient = containerClient.GetBlobClient(newPath);

            // Fetch properties and metadata of the source blob
            var properties = await sourceBlobClient.GetPropertiesAsync();

            // Download the source blob to a stream
            using (var sourceStream = await sourceBlobClient.OpenReadAsync())
            {
                // Create the blob at the new path with the stream, content type, and metadata
                var uploadOptions = new BlobUploadOptions
                {
                    HttpHeaders = new BlobHttpHeaders
                    {
                        ContentType = properties.Value.ContentType
                    },
                    Metadata = properties.Value.Metadata
                };
                await destinationBlobClient.UploadAsync(sourceStream, uploadOptions);
            }

            if (deleteOld)
            {
                // Delete the original blob
                await sourceBlobClient.DeleteIfExistsAsync();
            }
        }


        public async Task<Stream> DownloadAsync(string containerName, string path)
        {
            Logger.DihDebug("DownloadAsync");
            ValidateContainerAndPath(containerName, path);

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(path);

            return await blobClient.OpenReadAsync();
        }

        public async Task DeleteAsync(string containerName, string path)
        {
            Logger.DihDebug("DeleteAsync");
            ValidateContainerAndPath(containerName, path);

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(path);

            try
            {
                var response = await blobClient.DeleteAsync();
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                Logger.DihWarning($"Delete of {containerName}/{path} returned HTTP 404 - file no longer exists.");
            }
        }

        public async Task SetMetadataAsync(string containerName, string path, IDictionary<string, string> metadata)
        {
            Logger.DihDebug("SetMetadataAsync");
            ValidateContainerAndPath(containerName, path);
            ArgumentNullException.ThrowIfNull(metadata, nameof(metadata));

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(path);

            var blobProperties = await blobClient.GetPropertiesAsync();

            foreach (var item in metadata)
            {
                blobProperties.Value.Metadata[item.Key] = item.Value;
            }

            await blobClient.SetMetadataAsync(blobProperties.Value.Metadata);
        }

        public async Task SetMetadataAsync(string containerName, string path, object metadataObj)
        {
            Logger.DihDebug("SetMetadataAsync");
            ValidateContainerAndPath(containerName, path);
            ArgumentNullException.ThrowIfNull(metadataObj, nameof(metadataObj));

            var dictionary = metadataObj.GetType().GetProperties()
                .ToDictionary(property => property.Name, property => property.GetValue(metadataObj)?.ToString() ?? "");

            await SetMetadataAsync(containerName, path, dictionary);
        }

        private void ValidateContainerAndPath(string containerName, string path)
        {
            if (string.IsNullOrEmpty(containerName)) throw new ArgumentNullException(nameof(containerName));
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
        }

        public ValueTask DisposeAsync()
        {
            // Nothing to dispose
            return ValueTask.CompletedTask;
        }
    }
}


