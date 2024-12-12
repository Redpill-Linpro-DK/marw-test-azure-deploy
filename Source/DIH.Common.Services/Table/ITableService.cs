namespace DIH.Common.Services.Table
{
    /// <summary>
    /// Provide simple partition-key/row-key and partition-key/row-key/object operations.
    /// 
    /// Table names must exist prior to use. To create tables, add code to the project's IaC.
    /// </summary>
    public interface ITableService : IAsyncDisposable
    {
        /// <summary>
        /// Adds a row to a table with the given partition-key and row-key
        /// </summary>
        Task UpsertAsync(string tableName, string partitionKey, string rowKey);

        /// <summary>
        /// Adds a object row to a table with the given partition-key and row-key
        /// </summary>
        Task UpsertObjectAsync<T>(string tableName, string partitionKey, string rowKey, T obj) where T : new();

        /// <summary>
        /// Adds multiple row keys to a table with the given partition-key
        /// </summary>
        Task<int> UpsertBulkAsync(string tableName, string partitionKey, IAsyncEnumerable<string> rowKey, int maxParallelTasks);

        /// <summary>
        /// Checks if a row has been added to a table with the given partition-key and row-key
        /// </summary>
        /// <returns>True if the row exists</returns>
        Task<bool> ExistsAsync(string tableName, string partitionKey, string rowKey);

        /// <summary>
        /// Returns all row keys that has been added to the tables partition-key
        /// </summary>
        IAsyncEnumerable<string> GetRowKeysAsync(string tableName, string partitionKey);

        /// <summary>
        /// Returns the object with the given partition-key and row-key
        /// </summary>
        Task<T> TryGetObjectAsync<T>(string tableName, string partitionKey, string rowKey) where T : new();

        /// <summary>
        /// Returns all object rows that has been added to the tables partition-key
        /// </summary>
        IAsyncEnumerable<T> GetObjectsAsync<T>(string tableName, string partitionKey) where T : new();

        /// <summary>
        /// Returns all object rows from a table that are older than a specific timespan
        /// </summary>
        IAsyncEnumerable<T> GetOldObjectsAsync<T>(string tableName, TimeSpan olderThan) where T : new();

        /// <summary>
        /// Deletes a row from a table with the given partition-key and row-key
        /// </summary>
        Task DeleteAsync(string tableName, string partitionKey, string rowKey);

        /// <summary>
        /// Deletes multiple rows from a table with the given partition-key and the provided row-keys
        /// </summary>
        Task<int> DeleteBulkAsync(string tableName, string partitionKey, IAsyncEnumerable<string> rowKeys, int maxParallelism);

        /// <summary>
        /// Deletes rows from a table that have not been changed for the given time interval provided
        /// </summary>
        Task<int> DeleteOldRecordsAsync(string tableName, TimeSpan ageLimit, int maxParallelism);
    }
}


