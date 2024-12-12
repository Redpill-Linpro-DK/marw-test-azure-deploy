namespace DIH.Common.Services.Storage
{
    /// <summary>
    /// Provide simple file operations.
    /// </summary>
    public interface IStorageService : IAsyncDisposable
    {
        /// <summary>
        /// Upload a file from a stream.
        /// </summary>
        /// <param name="containerName">The name of container where the file should be stored</param>
        /// <param name="path">The path of the file, within its container</param>
        /// <param name="stream">A readable stream that represent the file content</param>
        /// <param name="contentType">Optional. The content type of the file, used as meta data</param>
        Task UploadAsync(string containerName, string path, Stream stream, string contentType = ContentTypes.Unknown);

        /// <summary>
        /// Move or copy a file. 
        /// 
        /// You can only move files within the same container.
        /// </summary>
        /// <param name="containerName">The name of container where the file is, and should be moved to</param>
        /// <param name="oldPath">Where the file is located within the container</param>
        /// <param name="newPath">Where the file should be moved to</param>
        /// <param name="deleteOld">True if the original location should be deleted. False = copy</param>
        Task MoveAsync(string containerName, string oldPath, string newPath, bool deleteOld = true);

        /// <summary>
        /// Returns a stream to the file at the provided location.
        /// </summary>
        /// <param name="containerName">The name of container where the file is</param>
        /// <param name="path">Where the file is located within the container</param>
        /// <returns>Stream to the content of the file</returns>
        Task<Stream> DownloadAsync(string containerName, string path);

        /// <summary>
        /// Deletes a file.
        /// </summary>
        /// <param name="containerName">The name of container where the file is</param>
        /// <param name="path">Where the file is located within the container</param>
        Task DeleteAsync(string containerName, string path);

        /// <summary>
        /// Sets meta data (key/value pairs) on a file.
        /// </summary>
        /// <param name="containerName">The name of container where the file is</param>
        /// <param name="path">Where the file is located within the container</param>
        /// <param name="metadata">Meta data represented as a dictionary</param>
        Task SetMetadataAsync(string containerName, string path, IDictionary<string, string> metadata);

        /// <summary>
        /// Sets meta data (key/value pairs) on a file.
        /// </summary>
        /// <param name="containerName">The name of container where the file is</param>
        /// <param name="path">Where the file is located within the container</param>
        /// <param name="metadataObj">Meta data represented as n object. The object's property names and values is used.</param>
        Task SetMetadataAsync(string containerName, string path, object metadataObj);
    }
}

