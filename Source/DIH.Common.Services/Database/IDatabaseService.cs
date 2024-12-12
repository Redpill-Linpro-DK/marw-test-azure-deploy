using Newtonsoft.Json.Linq;

namespace DIH.Common.Services.Database
{
    public interface IDatabaseService : IAsyncDisposable
    {
        /// <summary>
        /// Get a single data object of type T, having the specified DataObjectIdentifier (id / row key), read from the store identified by dataObjectTypeName
        /// </summary>
        /// <typeparam name="T">The data type to return</typeparam>
        /// <param name="dataObjectTypeName">A string that identifies the sotre where the data object should be read from</param>
        /// <param name="dataObjectIdentifier"></param>
        /// <returns>Use await. An instance of T or NULL</returns>
        Task<T?> GetByIdentifierAsync<T>(string dataObjectTypeName, DataObjectIdentifier dataObjectIdentifier) where T : class;

        /// <summary>
        /// Get multiple data objects of type T, having one of the specified DataObjectIdentifier (id / row key), read from the store identified by dataObjectTypeName
        /// </summary>
        /// <typeparam name="T">The data type to return</typeparam>
        /// <param name="dataObjectTypeName">A string that identifies the sotre where the data object should be read from</param>
        /// <param name="dataObjectIdentifiers"></param>
        /// <returns>Data objects matching provided list of DataObjectIdentifier.</returns>
        IAsyncEnumerable<T> GetByIdentifiersAsync<T>(string dataObjectTypeName, IAsyncEnumerable<DataObjectIdentifier> dataObjectIdentifiers) where T : class;

        /// <summary>
        /// Gets all data objects of type T, satisfying the query (or all if no query), read from the store identified by dataObjectTypeName
        /// 
        /// Example query formats:
        ///  - field1 = '3' OR field2 = false
        ///  - field1 >= 1 OR field2 <> 'cat'
        /// </summary>
        /// <typeparam name="T">The data type to return, a domain class matching data or JObject</typeparam>
        /// <param name="dataObjectTypeName">A string that identifies the sotre where the data object should be read from</param>
        /// <param name="queryFilter">Optional. A string like "field1 = '3' OR field2 = false" that filters the result.</param>
        /// <param name="zeroBasedSegment">The zero-based segment number to fetch.</param>
        /// <param name="segmentsTotal">The total number of segments. Must be higher than zeroBasedSegment.</param>
        /// <returns>A lazy IAsyncEnumerable<T> that deliveres objects as you await them.</returns>
        IAsyncEnumerable<T> GetByQueryAsync<T>(string dataObjectTypeName, string? queryFilter = null, int zeroBasedSegment = 0, int segmentsTotal = 1);

        /// <summary>
        /// Continuation token based paging.
        /// 
        /// Gets a paged subset of data objects of type T, satisfying the query (or all if no query), read from the store identified by dataObjectTypeName.
        /// 
        /// The returned <see cref="ITokenPagedDatabaseResult{T}"/> contain the result and continuation token to use to get further results (next page).
        /// Example query formats:
        ///  - field1 = '3' OR field2 = false
        ///  - field1 >= 1 OR field2 <> 'cat'
        /// </summary>
        /// <typeparam name="T">The data type to return</typeparam>
        /// <param name="dataObjectTypeName">A string that identifies the sotre where the data object should be read from</param>
        /// <param name="pageSize">The (max) number of objects to return</param>
        /// <param name="queryFilter">Optional. A string like "field1 = '3' OR field2 = false" that filters the result.</param>
        /// <param name="continuationToken">For next page, provide the <see cref="ITokenPagedDatabaseResult{T}.ContinuationToken"/> from the last page. NULL or empty string for the first page.</param>
        /// <returns>A lazy <see cref="ITokenPagedDatabaseResult{T}"/> that contain the result and the next <see cref="ITokenPagedDatabaseResult{T}.ContinuationToken"/>.</returns>
        Task<ITokenPagedDatabaseResult<T>> GetPagedByQueryAsync<T>(string dataObjectTypeName, int pageSize, string? queryFilter = null, string? continuationToken = null) where T : class;

        /// <summary>
        /// Index based paging.
        /// 
        /// Gets a paged subset of data objects of type T, satisfying the query (or all if no query), read from the store identified by dataObjectTypeName.
        /// 
        /// The returned <see cref="IIndexPagedDatabaseResult{T}"/> contain the result and details about page numbers, counts and index details.
        /// Example query formats:
        ///  - field1 = '3' OR field2 = false
        ///  - field1 >= 1 OR field2 <> 'cat'
        /// </summary>
        /// <typeparam name="T">The data type to return</typeparam>
        /// <param name="dataObjectTypeName">A string that identifies the sotre where the data object should be read from</param>
        /// <param name="pageSize">The (max) number of objects to return</param>
        /// <param name="pageIndex">0 (zero) based page index to get.</param>
        /// <param name="queryFilter">Optional. A string like "field1 = '3' OR field2 = false" that filters the result.</param>
        /// <returns>A lazy <see cref="IIndexPagedDatabaseResult{T}"/> that contain the result and page/count details for the result.</returns>
        Task<IIndexPagedDatabaseResult<T>> GetPagedByQueryAsync<T>(string dataObjectTypeName, int pageSize, int pageIndex = 0, string? queryFilter = null) where T : class;


        /// <summary>
        /// Upserts a single item into the database.
        /// </summary>
        /// <param name="dataObjectTypeName">The type name of the data object to upsert.</param>
        /// <param name="item">The item to upsert.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UpsertAsync(string dataObjectTypeName, object item);

        /// <summary>
        /// Upserts multiple <typeparamref name="T"/> items into the database in parallel.
        /// </summary>
        /// <param name="dataObjectTypeName">The type name of the data objects to upsert.</param>
        /// <param name="items">The items to upsert.</param>
        /// <param name="maxParallelTasks">The maximum number of parallel tasks.</param>
        /// <returns>The number of upserted items.</returns>
        Task<int> UpsertBulkAsync<T>(string dataObjectTypeName, IAsyncEnumerable<T> items, int maxParallelTasks) where T : class;

        /// <summary>
        /// Fetches a segment of data object IDs that match the given query.
        /// Example query formats:
        ///  - field1 = '3' OR field2 = false
        ///  - field1 >= 1 OR field2 <> 'cat'
        /// </summary>
        /// <param name="dataObjectTypeName">The type name of the data object to query.</param>
        /// <param name="queryFilter">The query that data must satisfy, e.g., __DIH_BATCH_ID = 'someid'.</param>
        /// <param name="zeroBasedSegment">The zero-based segment number to fetch.</param>
        /// <param name="segmentsTotal">The total number of segments. Must be higher than zeroBasedSegment.</param>
        /// <returns>An asynchronous sequence of data object IDs.</returns>
        IAsyncEnumerable<DataObjectIdentifier> GetIdentifiersAsync(string dataObjectTypeName, string? queryFilter = null, int zeroBasedSegment = 0, int segmentsTotal = 1);

        /// <summary>
        /// Retrieves the count of data objects matching the specified query. If no query is provided, the total count is returned.
        /// </summary>
        /// <param name="dataObjectTypeName">The type name of the data object to query.</param>
        /// <param name="queryFilter">The query that data must satisfy, e.g., __DIH_BATCH_ID = 'someid'.</param>
        /// <returns>The count of matching data objects.</returns>
        Task<int> GetCountAsync(string dataObjectTypeName, string? queryFilter = null);

        /// <summary>
        /// Deletes a data object from the database based on its ID.
        /// </summary>
        /// <param name="dataObjectTypeName">The type name of the data object to delete.</param>
        /// <param name="identifier">The DataObjectIdentifier (id, row key) of the data object to delete.</param>
        /// <returns>A task representing the asynchronous delete operation.</returns>
        Task DeleteAsync(string dataObjectTypeName, DataObjectIdentifier identifier);

        /// <summary>
        /// Deletes multiple data objects from the database based on their IDs.
        /// </summary>
        /// <param name="dataObjectTypeName">The type name of the data objects to delete.</param>
        /// <param name="identifiers">list of DataObjectIdentifier (id, partition key) of the data objects to delete.</param>
        /// <param name="maxParallelTasks">The maximum number of parallel tasks.</param>
        /// <returns>The number of deleted data objects.</returns>
        Task<int> DeleteBulkAsync(string dataObjectTypeName, IAsyncEnumerable<DataObjectIdentifier> identifiers, int maxParallelTasks);

        /// <summary>
        /// Returns the names of all Data Object Type Names known by the database
        /// </summary>
        /// <returns>Names of data object types</returns>
        Task<IEnumerable<string>> GetDataObjectTypeNamesAsync();


        /// <summary>
        /// Returns a DataObjectIdentifier (Id, PartitionKey) that identifies the provided data object.
        /// </summary>
        /// <param name="dataObjectTypeName">The type name of the data objects to delete.</param>
        /// <param name="dataObject">An instance of the data object to get identifier for</param>
        /// <returns>DataObjectIdentifier (Id, PartitionKey) that identifies the provided data object.</returns>
        DataObjectIdentifier GetDataObjectIdentifier(string dataObjectTypeName, JObject dataObject);

        /// <summary>
        /// returns the identifier and partition key for a given data object of type T. 
        /// This method uses reflection to find properties marked with JsonProperty attributes that match the configured
        /// keys for the data object type name. It supports handling properties of various types by converting them
        /// to their string representations.
        ///</summary>
        /// <typeparam name="T">The type of the data object. Must be a class.</typeparam>
        /// <param name="dataObjectTypeName">The type name of the data object as configured in the system. 
        /// This is used to fetch the partition key name from the configuration.</param>
        /// <param name="dataObject">The instance of the data object from which to extract the identifier and partition key.</param>
        /// <returns>A <see cref="DataObjectIdentifier"/> containing the ID and partition key of the data object.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the ID or the partition key is null after attempting to retrieve their values.</exception>
        DataObjectIdentifier GetDataObjectIdentifier<T>(string dataObjectTypeName, T dataObject) where T : class;
    }
}


