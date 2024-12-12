namespace DIH.Common.Services.Storage
{
    /// <summary>
    /// Contains IStorageService instances to access files from stores of diffferent layers.
    /// </summary>
    public interface IStorageServiceDictionary : IAsyncDisposable
    {
        /// <summary>
        /// Returna a IStorageService instance to access files from the specified layer
        /// </summary>
        /// <param name="name">The layer</param>
        /// <returns>Storage service providing file access</returns>
        IStorageService Get(Layer name);
    }
}


