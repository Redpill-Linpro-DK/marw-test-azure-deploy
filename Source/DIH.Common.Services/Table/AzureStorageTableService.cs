using Azure;
using Azure.Data.Tables;
using DIH.Common.Tasks;
using System.Collections.Concurrent;
using DIH.Common.Credential;

namespace DIH.Common.Services.Table
{
    public class AzureTableStorageService : ITableService
    {
        private readonly TableServiceClient _serviceClient;
        private readonly ConcurrentDictionary<string, TableClient> _tableClientCache;

        public AzureTableStorageService(string tableServiceEndpoint)
        {
            if (string.IsNullOrEmpty(tableServiceEndpoint)) throw new ArgumentNullException(nameof(tableServiceEndpoint));

            _serviceClient = new TableServiceClient(new Uri(tableServiceEndpoint), AzureCredentialFactory.BuildDefault());
            _tableClientCache = new ConcurrentDictionary<string, TableClient>();
        }

        public async Task UpsertAsync(string tableName, string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(partitionKey)) throw new ArgumentNullException(nameof(partitionKey));
            if (string.IsNullOrEmpty(rowKey)) throw new ArgumentNullException(nameof(rowKey));

            var tableClient = GetTableClient(tableName);
            TableEntity entity = new TableEntity(partitionKey, rowKey);

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        }

        public async Task UpsertObjectAsync<T>(string tableName, string partitionKey, string rowKey, T obj) where T : new()
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(partitionKey)) throw new ArgumentNullException(nameof(partitionKey));
            if (string.IsNullOrEmpty(rowKey)) throw new ArgumentNullException(nameof(rowKey));
            ArgumentNullException.ThrowIfNull(obj, nameof(obj));

            var tableClient = GetTableClient(tableName);
            var entity = new ObjectTableEntity<T>(partitionKey, rowKey, obj);

            await tableClient.UpsertEntityAsync(entity, TableUpdateMode.Replace);
        }

        public async Task<int> UpsertBulkAsync(string tableName, string partitionKey, IAsyncEnumerable<string> rowKeys, int maxParallelTasks)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(partitionKey)) throw new ArgumentNullException(nameof(partitionKey));
            ArgumentNullException.ThrowIfNull(rowKeys, nameof(rowKeys));

            var upsertCount = 0;

            await using (var bulkAwaiter = new BulkTaskAwaiter(maxParallelTasks))
            {
                await foreach (var rowKey in rowKeys)
                {
                    await bulkAwaiter.Add(
                        UpsertAsync(tableName, partitionKey, rowKey)
                    );
                    upsertCount += 1;
                }
            }

            return upsertCount;
        }

        public async Task<bool> ExistsAsync(string tableName, string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(partitionKey)) throw new ArgumentNullException(nameof(partitionKey));
            if (string.IsNullOrEmpty(rowKey)) throw new ArgumentNullException(nameof(rowKey));

            var tableClient = GetTableClient(tableName);
            var response = await tableClient.GetEntityIfExistsAsync<TableEntity>(partitionKey, rowKey);

            return response.HasValue;
        }

        public IAsyncEnumerable<string> GetRowKeysAsync(string tableName, string partitionKey)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(partitionKey)) throw new ArgumentNullException(nameof(partitionKey));

            var tableClient = GetTableClient(tableName);
            var query = tableClient.QueryAsync<TableEntity>(e => e.PartitionKey == partitionKey);

            return query.Select(e => e.RowKey);
        }

        public async Task<T> TryGetObjectAsync<T>(string tableName, string partitionKey, string rowKey) where T : new()
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(partitionKey)) throw new ArgumentNullException(nameof(partitionKey));
            if (string.IsNullOrEmpty(rowKey)) throw new ArgumentNullException(nameof(rowKey));

            var tableClient = GetTableClient(tableName);
            var response = await tableClient.GetEntityIfExistsAsync<ObjectTableEntity<T>>(partitionKey, rowKey);

#pragma warning disable CS8603 // Possible null reference return.
            return response.HasValue && response.Value != null ? response.Value.GetObject() : default(T);
#pragma warning restore CS8603 // Possible null reference return.
        }

        public IAsyncEnumerable<T> GetObjectsAsync<T>(string tableName, string partitionKey) where T : new()
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(partitionKey)) throw new ArgumentNullException(nameof(partitionKey));

            var tableClient = GetTableClient(tableName);
            var query = tableClient.QueryAsync<ObjectTableEntity<T>>(e => e.PartitionKey == partitionKey);

            return query.Select(e => e.GetObject());
        }

        public IAsyncEnumerable<T> GetOldObjectsAsync<T>(string tableName, TimeSpan olderThan) where T : new()
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            var tableClient = GetTableClient(tableName);
            var thresholdDate = DateTimeOffset.UtcNow.Subtract(olderThan);
            var filter = TableClient.CreateQueryFilter($"Timestamp lt {thresholdDate:o}");

            var query = tableClient.QueryAsync<ObjectTableEntity<T>>(filter);

            return query.Select(e => e.GetObject());
        }

        public async Task DeleteAsync(string tableName, string partitionKey, string rowKey)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(partitionKey)) throw new ArgumentNullException(nameof(partitionKey));
            if (string.IsNullOrEmpty(rowKey)) throw new ArgumentNullException(nameof(rowKey));

            try
            {
                var tableClient = GetTableClient(tableName);
                await tableClient.DeleteEntityAsync(partitionKey, rowKey);
            }
            catch (RequestFailedException ex)
            {
                if (ex.Status != 404)
                    throw;
            }
        }

        public async Task<int> DeleteBulkAsync(string tableName, string partitionKey, IAsyncEnumerable<string> rowKeys, int maxParallelism)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));
            if (string.IsNullOrEmpty(partitionKey)) throw new ArgumentNullException(nameof(partitionKey));
            ArgumentNullException.ThrowIfNull(rowKeys, nameof(rowKeys));

            var deleteCount = 0;

            await using (var bulkAwaiter = new BulkTaskAwaiter(maxParallelism))
            {
                await foreach (var rowKey in rowKeys)
                {
                    await bulkAwaiter.Add(
                        DeleteAsync(tableName, partitionKey, rowKey)
                    );
                    deleteCount += 1;
                }
            }

            return deleteCount;
        }

        public async Task<int> DeleteOldRecordsAsync(string tableName, TimeSpan olderThan, int maxParallelism)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            var tableClient = GetTableClient(tableName);
            var thresholdDate = DateTimeOffset.UtcNow.Subtract(olderThan);
            var filter = TableClient.CreateQueryFilter($"Timestamp lt {thresholdDate:o}");

            var query = tableClient.QueryAsync<TableEntity>(filter);
            var deleteCount = 0;

            await using (var bulkAwaiter = new BulkTaskAwaiter(maxParallelism))
            {
                await foreach (var entity in query)
                {
                    await bulkAwaiter.Add(
                        tableClient.DeleteEntityAsync(entity.PartitionKey, entity.RowKey)
                    );
                    deleteCount += 1;
                }
            }

            return deleteCount;
        }

        private TableClient GetTableClient(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) throw new ArgumentNullException(nameof(tableName));

            return _tableClientCache.GetOrAdd(tableName, key =>
            {
                return _serviceClient.GetTableClient(key);
            });
        }

        public ValueTask DisposeAsync()
        {
            // Nothing to dispose
            return ValueTask.CompletedTask;
        }
    }
}


