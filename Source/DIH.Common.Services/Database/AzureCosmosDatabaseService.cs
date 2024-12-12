using DIH.Common.Services.Database.Helpers;
using DIH.Common.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using DIH.Common.Credential;

namespace DIH.Common.Services.Database
{
    public class AzureCosmosDatabaseService : IDatabaseService
    {
        private readonly string _databaseName;
        private readonly CosmosClient _client;
        private readonly DihLogger<AzureCosmosDatabaseService> Logger;
        private readonly Layer _layer;
        private readonly IConfiguration _configuration;
        private const int MaxRetryAttempts = 10; // For upserts and deletes, number of retries.
        private const int RetryDelayMs = 250;  // For upserts and deletes, exponential use: Delay = RetryDelayMs * (int)Math.Pow(2, retryCount)
        private readonly HttpStatusCode[] retryStatusCodes =
        {
            HttpStatusCode.TooManyRequests,     // 429
            HttpStatusCode.ServiceUnavailable,  // 503
            HttpStatusCode.RequestTimeout,      // 408
            HttpStatusCode.PreconditionFailed,  // 412
            HttpStatusCode.InternalServerError  // 500
        };
        private readonly ConcurrentDictionary<Type, PropertyInfo> idPropertyCache = new ConcurrentDictionary<Type, PropertyInfo>();
        private readonly ConcurrentDictionary<Type, PropertyInfo> partitionKeyPropertyCache = new ConcurrentDictionary<Type, PropertyInfo>();


        public AzureCosmosDatabaseService(ILogger<AzureCosmosDatabaseService> logger, IConfiguration configuration, string cosmosDbAccountEndpoint, Layer layer)
        {
            ArgumentNullException.ThrowIfNull(cosmosDbAccountEndpoint, nameof(cosmosDbAccountEndpoint));

            Logger = logger.AsDihLogger();

            _databaseName = layer switch
            {
                Layer.DataRaw => DatabaseNames.Raw,
                Layer.DataPrepared => DatabaseNames.Prepared,
                _ => throw new ArgumentException($"No database defined for layer {layer}")
            };

            _configuration = configuration;
            _layer = layer;

            // Local dev firewall workaround - can be activated via config
            var devOlyUseStandardConnectionsConfig = configuration[ConfigKeys.DIH_Developer_OnlyUseStandardConnections];
            var useStandardConnection = string.IsNullOrEmpty(devOlyUseStandardConnectionsConfig) == false && bool.Parse(devOlyUseStandardConnectionsConfig);

            if (useStandardConnection)
            {
                // Slower 
                _client = new CosmosClientBuilder(cosmosDbAccountEndpoint, AzureCredentialFactory.BuildDefault())
                                .WithConnectionModeGateway()
                                .WithBulkExecution(true)
                                .Build();
            }
            else
            {
                // Desired
                _client = new CosmosClientBuilder(cosmosDbAccountEndpoint, AzureCredentialFactory.BuildDefault())
                                .WithConnectionModeDirect()
                                .WithBulkExecution(true)
                                .Build();
            }
        }
        public async Task<T?> GetByIdentifierAsync<T>(string dataObjectTypeName, DataObjectIdentifier dataObjectIdentifier) where T : class
        {
            var partitionKeyName = _configuration[ConfigKeys.DataPartitionKey(_layer, dataObjectTypeName)];

            var query = $"{JsonPropertyNames.Id} = '{dataObjectIdentifier.Id.Replace("'", "''")}' " +
                        $"AND {partitionKeyName} = '{dataObjectIdentifier.PartitionKey.Replace("'", "''")}'";

            return await GetByQueryAsync<T>(dataObjectTypeName, query).FirstOrDefaultAsync();
        }

        public IAsyncEnumerable<T> GetByIdentifiersAsync<T>(string dataObjectTypeName, IAsyncEnumerable<DataObjectIdentifier> dataObjectIdentifiers) where T : class
        {
            var partitionKeyName = _configuration[ConfigKeys.DataPartitionKey(_layer, dataObjectTypeName)];

            var query = string.Join(" OR ", dataObjectIdentifiers.ToEnumerable().Select(dataObjectIdentifier =>
                        $"{JsonPropertyNames.Id} = '{dataObjectIdentifier.Id.Replace("'", "''")}' " +
                        $"AND {partitionKeyName} = '{dataObjectIdentifier.PartitionKey.Replace("'", "''")}'"));

            return GetByQueryAsync<T>(dataObjectTypeName, query);
        }

        public async IAsyncEnumerable<T> GetByQueryAsync<T>(string dataObjectTypeName, string? queryFilter = null, int zeroBasedSegment = 0, int segmentsTotal = 1)
        {
            ArgumentNullException.ThrowIfNull(dataObjectTypeName, nameof(dataObjectTypeName));
            if (!(zeroBasedSegment < segmentsTotal && zeroBasedSegment > -1)) throw new ArgumentOutOfRangeException(nameof(zeroBasedSegment));

            Logger.DihDebug("GetByQueryAsync");

            var container = _client.GetContainer(_databaseName, dataObjectTypeName);

            var cosmosQuery =
                $"SELECT * " +
                $"FROM c " +
                $"WHERE {(segmentsTotal > 1 ? $"DocumentId(c) % {segmentsTotal} = {zeroBasedSegment} " : "")}" +
                (segmentsTotal > 1 && !String.IsNullOrEmpty(queryFilter) ? "AND " : "") +
                (String.IsNullOrEmpty(queryFilter) ? "" : $"({QueryConverter.ForCosmosDB(queryFilter)})");

            if (cosmosQuery.EndsWith("WHERE ")) cosmosQuery = cosmosQuery + "true";

            var iterator = container.GetItemQueryIterator<T>(cosmosQuery);

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                foreach (var item in response)
                {
                    yield return item;
                }
            }
        }

        public async Task<ITokenPagedDatabaseResult<T>> GetPagedByQueryAsync<T>(string dataObjectTypeName, int pageSize, string? queryFilter = null, string? continuationToken = null) where T : class
        {
            ArgumentNullException.ThrowIfNull(dataObjectTypeName, nameof(dataObjectTypeName));
            if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize));

            Logger.DihDebug("GetByQueryAsync");

            var container = _client.GetContainer(_databaseName, dataObjectTypeName);

            var cosmosQuery =
                $"SELECT * " +
                $"FROM c " +
                (String.IsNullOrEmpty(queryFilter) ? "" : $"WHERE {QueryConverter.ForCosmosDB(queryFilter)}");

            var iterator = container.GetItemQueryIterator<T>(
                cosmosQuery,
                continuationToken: continuationToken, // Send continuationToken to Cosmos
                requestOptions: new QueryRequestOptions
                {
                    MaxItemCount = pageSize
                });

            var resultsPage = new List<T>();
            string? nextContinuationToken = null;
            while (iterator.HasMoreResults && resultsPage.Count < pageSize)
            {
                var result = await iterator.ReadNextAsync();
                resultsPage.AddRange(result);
                nextContinuationToken = result.ContinuationToken;
            }

            return new TokenPagedDatabaseResult<T>(resultsPage.ToAsyncEnumerable(), nextContinuationToken);
        }

        public async Task<IIndexPagedDatabaseResult<T>> GetPagedByQueryAsync<T>(string dataObjectTypeName, int pageSize, int itemOffset = 0, string? queryFilter = null) where T : class
        {
            ArgumentNullException.ThrowIfNull(dataObjectTypeName, nameof(dataObjectTypeName));
            if (itemOffset < 0) throw new ArgumentOutOfRangeException(nameof(itemOffset));
            if (pageSize < 1) throw new ArgumentOutOfRangeException(nameof(pageSize));

            Logger.DihDebug($"GetPagedByQueryAsync {dataObjectTypeName}, item offset {itemOffset}, page size {pageSize}");

            var totalCount = await GetCountAsync(dataObjectTypeName, queryFilter);

            // Return / throw on empty / bad page index
            if (totalCount == 0) return new IndexPagedDatabaseResult<T>(new List<T>(), pageSize, itemOffset, totalCount);
            if (itemOffset >= totalCount) throw new InvalidOperationException($"The specified itemOffset ({itemOffset}) is out of range. Note that the pageIndex parameter is zero-based. Max index is {Math.Ceiling((decimal)totalCount / pageSize) - 1:0}.");

            // Fetch data
            var container = _client.GetContainer(_databaseName, dataObjectTypeName);
            var cosmosQuery = $"SELECT * " +
                              $"FROM c " +
                              $"{(String.IsNullOrEmpty(queryFilter) ? "" : $"WHERE {QueryConverter.ForCosmosDB(queryFilter)} ")}" +
                              $"OFFSET {itemOffset} LIMIT {pageSize}";

            var iterator = container.GetItemQueryIterator<T>(cosmosQuery);

            var resultsPage = new List<T>();
            while (iterator.HasMoreResults)
            {
                var result = await iterator.ReadNextAsync();
                resultsPage.AddRange(result);
            }

            // Construct the result with totalCount included
            return new IndexPagedDatabaseResult<T>(resultsPage, pageSize, itemOffset, totalCount);
        }



        public async Task UpsertAsync(string dataObjectTypeName, object item)
        {
            ArgumentNullException.ThrowIfNull(dataObjectTypeName, nameof(dataObjectTypeName));
            ArgumentNullException.ThrowIfNull(item, nameof(item));
            ValidateIdProperty(dataObjectTypeName, item);

            Logger.DihDebug("UpsertAsync");
            var container = _client.GetContainer(_databaseName, dataObjectTypeName);

            await ExecuteWithRetriesAsync(async () =>
            {
                await container.UpsertItemAsync(item);
            }, $"Upsert {container.Id}");
        }

        public async Task<int> UpsertBulkAsync<T>(string dataObjectTypeName, IAsyncEnumerable<T> items, int maxParallelTasks) where T : class
        {
            ArgumentNullException.ThrowIfNull(dataObjectTypeName, nameof(dataObjectTypeName));
            ArgumentNullException.ThrowIfNull(items, nameof(items));

            Logger.DihDebug($"UpsertBulkAsync<{typeof(T).Name}>");
            var upsertCount = 0;
            var container = _client.GetContainer(_databaseName, dataObjectTypeName);

            await using (var taskAwaiter = new BulkTaskAwaiter(awaitAtMaxTasks: maxParallelTasks, maxProcessorLoadPct: 75, maxCpuCoolWaitSec: 5))
            {
                await foreach (var item in items)
                {
                    await taskAwaiter.Add(DoUpsertAsync(container, dataObjectTypeName, item));
                    upsertCount += 1;
                }
            }

            return upsertCount;
        }

        private async Task DoUpsertAsync<T>(Container container, string dataObjectTypeName, T item)
        {
            ValidateIdProperty(dataObjectTypeName, item);
            var partitionKeyValue = GetPartitionKeyValue(dataObjectTypeName, item);
            await ExecuteWithRetriesAsync(async () =>
            {
                await container.UpsertItemAsync(item, new PartitionKey(partitionKeyValue));
            }, $"Upsert {container.Id}, partition {partitionKeyValue}");
        }

        public async IAsyncEnumerable<DataObjectIdentifier> GetIdentifiersAsync(string dataObjectTypeName, string? queryFilter = null, int zeroBasedSegment = 0, int segmentsTotal = 1)
        {
            ArgumentNullException.ThrowIfNull(dataObjectTypeName, nameof(dataObjectTypeName));
            if (!(zeroBasedSegment < segmentsTotal && zeroBasedSegment > -1)) throw new ArgumentOutOfRangeException(nameof(zeroBasedSegment));

            Logger.DihDebug("GetIdentifiersAsync");

            var container = _client.GetContainer(_databaseName, dataObjectTypeName);
            var partitionKeyName = _configuration[ConfigKeys.DataPartitionKey(_layer, dataObjectTypeName)];

            await foreach (var id in GetIdentifiersSegmentAsync(container, partitionKeyName, queryFilter, zeroBasedSegment, segmentsTotal))
            {
                yield return id;
            }
        }

        private async IAsyncEnumerable<DataObjectIdentifier> GetIdentifiersSegmentAsync(Container container, string partitionKeyName, string? queryFilter, int segment, int segmentsTotal)
        {
            var cosmosQuery =
                $"SELECT c.{JsonPropertyNames.Id} As Id, c.{partitionKeyName} As PartitionKey " +
                $"FROM c " +
                $"WHERE {(segmentsTotal > 1 ? $"DocumentId(c) % {segmentsTotal} = {segment} " : "")}" +
                (segmentsTotal > 1 && !String.IsNullOrEmpty(queryFilter) ? "AND " : "") +
                (String.IsNullOrEmpty(queryFilter) ? "" : $"({QueryConverter.ForCosmosDB(queryFilter)})");

            if (cosmosQuery.EndsWith("WHERE ")) cosmosQuery = cosmosQuery + "true";

            var iterator = container.GetItemQueryIterator<DataObjectIdentifier>(cosmosQuery);

            while (iterator.HasMoreResults)
            {
                var result = await iterator.ReadNextAsync();
                foreach (var item in result)
                {
                    yield return item;
                }
            }
        }

        public async Task<int> GetCountAsync(string dataObjectTypeName, string? queryFilter = null)
        {
            ArgumentNullException.ThrowIfNull(dataObjectTypeName, nameof(dataObjectTypeName));

            Logger.DihDebug("GetCountAsync");

            var container = _client.GetContainer(_databaseName, dataObjectTypeName);

            var cosmosQuery =
                $"SELECT VALUE COUNT(1) " +
                $"FROM c " +
                (String.IsNullOrEmpty(queryFilter) ? "" : $"WHERE {QueryConverter.ForCosmosDB(queryFilter)}");

            var iterator = container.GetItemQueryIterator<int>(cosmosQuery);

            var result = await iterator.ReadNextAsync();
            return result.FirstOrDefault();
        }

        public async Task DeleteAsync(string dataObjectTypeName, DataObjectIdentifier identifier)
        {
            ArgumentNullException.ThrowIfNull(dataObjectTypeName, nameof(dataObjectTypeName));
            ArgumentNullException.ThrowIfNull(identifier, nameof(identifier));

            var container = _client.GetContainer(_databaseName, dataObjectTypeName);
            await DoDeleteAsync(container, identifier);
        }

        public async Task<int> DeleteBulkAsync(string dataObjectTypeName, IAsyncEnumerable<DataObjectIdentifier> identifiers, int maxParallelTasks)
        {
            ArgumentNullException.ThrowIfNull(dataObjectTypeName, nameof(dataObjectTypeName));
            ArgumentNullException.ThrowIfNull(identifiers, nameof(identifiers));

            Logger.DihDebug("DeleteBulkAsync");
            var deleteCount = 0;
            var container = _client.GetContainer(_databaseName, dataObjectTypeName);

            await using (var taskAwaiter = new BulkTaskAwaiter(awaitAtMaxTasks: maxParallelTasks, maxProcessorLoadPct: 75, maxCpuCoolWaitSec: 5))
            {
                await foreach (DataObjectIdentifier identifier in identifiers)
                {
                    await taskAwaiter.Add(DoDeleteAsync(container, identifier));
                    deleteCount += 1;
                }
            }

            return deleteCount;
        }

        public async Task<IEnumerable<string>> GetDataObjectTypeNamesAsync()
        {
            Logger.DihDebug("ListContainerNamesAsync");

            var database = _client.GetDatabase(_databaseName);
            var containerList = new List<string>();
            var iterator = database.GetContainerQueryIterator<ContainerProperties>();

            while (iterator.HasMoreResults)
            {
                var response = await iterator.ReadNextAsync();
                foreach (var containerProperties in response)
                {
                    containerList.Add(containerProperties.Id);
                }
            }

            return containerList.AsEnumerable();
        }

        private async Task DoDeleteAsync(Container container, DataObjectIdentifier identifier)
        {
            await ExecuteWithRetriesAsync(async () =>
            {
                await container.DeleteItemAsync<object>(identifier.Id, new PartitionKey(identifier.PartitionKey));
            }, $"Delete {container.Id} {identifier.Id}/{identifier.PartitionKey}");
        }

        private async Task ExecuteWithRetriesAsync(Func<Task> action, string actionDescription)
        {
            var retryCount = 0;
            int retryDelayMs;
            while (retryCount < MaxRetryAttempts)
            {
                try
                {
                    await action();
                    Logger.DihDebug($"{actionDescription}");
                    return;
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                {
                    Logger.DihWarning($"{actionDescription} returned 404 (item already gone).");
                    return;
                }
                catch (Exception ex) when (ex is CosmosException cosEx && retryStatusCodes.Contains(cosEx.StatusCode) || ex is SocketException)
                {
                    // Throttling, retry after delay
                    retryCount++;
                    retryDelayMs = RetryDelayMs * (int)Math.Pow(2, retryCount);

                    var errorHint = ex is CosmosException cosmosEx ? $"HTTP {cosmosEx.StatusCode}" : $"Error: {ex.Message}";
                    Logger.DihTransientEvent($"{actionDescription}: {errorHint}", retryCount, MaxRetryAttempts, retryDelayMs);

                    await Task.Delay(retryDelayMs);
                }
                catch
                {
                    // Other exceptions, don't retry
                    throw;
                }
            }
            throw new Exception($"Max retry attempts reached. {actionDescription} failed.");
        }

        public ValueTask DisposeAsync()
        {
            _client.Dispose();
            return ValueTask.CompletedTask;
        }

        public DataObjectIdentifier GetDataObjectIdentifier(string dataObjectTypeName, JObject dataObject)
        {
            var partitionKeyName = _configuration[ConfigKeys.DataPartitionKey(_layer, dataObjectTypeName)];

            var id = dataObject.Value<string>(JsonPropertyNames.Id) ?? throw new InvalidOperationException($"Unexpected null id value in {nameof(dataObject)}");
            var partitionKey = dataObject.Value<string>(partitionKeyName) ?? throw new InvalidOperationException($"Unexpected null {partitionKeyName} (partition key field) value in {nameof(dataObject)}");

            return new DataObjectIdentifier(id, partitionKey);
        }

        public DataObjectIdentifier GetDataObjectIdentifier<T>(string dataObjectTypeName, T dataObject) where T : class
        {
            var partitionKeyName = _configuration[ConfigKeys.DataPartitionKey(_layer, dataObjectTypeName)];

            var idProperty = idPropertyCache.GetOrAdd(typeof(T), type => FindPropertyByJsonPropertyName(type, JsonPropertyNames.Id));
            var partitionKeyProperty = partitionKeyPropertyCache.GetOrAdd(typeof(T), type => FindPropertyByJsonPropertyName(type, partitionKeyName));

            var id = Convert.ToString(idProperty.GetValue(dataObject)) ?? throw new InvalidOperationException($"Unexpected null id value in {nameof(dataObject)}");
            var partitionKey = Convert.ToString(partitionKeyProperty.GetValue(dataObject)) ?? throw new InvalidOperationException($"Unexpected null {partitionKeyName} (partition key field) value in {nameof(dataObject)} with id {id}");

            return new DataObjectIdentifier(id, partitionKey);
        }

        private PropertyInfo FindPropertyByJsonPropertyName(Type type, string jsonPropertyName)
        {
            foreach (var propertyInfo in type.GetProperties())
            {
                var jsonProperty = propertyInfo.GetCustomAttribute<JsonPropertyAttribute>();
                if (jsonProperty != null)
                {
                    Logger.LogInformation($"Inspecting property: {propertyInfo.Name}, JSON property: {jsonProperty.PropertyName}, Target: {jsonPropertyName}");
                    if (jsonProperty.PropertyName == jsonPropertyName)
                    {
                        return propertyInfo;
                    }
                }
            }
            throw new InvalidOperationException($"Could not locate property with JsonProperty name '{jsonPropertyName}' on data object type {type.Name}");
        }

        private void ValidateIdProperty<T>(string dataObjectTypeName, T dataObject)
        {
            if (dataObject is JObject jDataObject)
            {
                if (string.IsNullOrEmpty(jDataObject.Value<string>(JsonPropertyNames.Id))) throw new InvalidOperationException($"Could not locate property {JsonPropertyNames.Id} on data object type {typeof(T).Name}, {dataObjectTypeName}");
            }
            else
            {
                PropertyInfo targetProperty = idPropertyCache.GetOrAdd(typeof(T), (type) =>
                {
                    foreach (PropertyInfo property in type.GetProperties())
                    {
                        var jsonProperty = property.GetCustomAttribute<JsonPropertyAttribute>();
                        if (jsonProperty != null && jsonProperty.PropertyName == JsonPropertyNames.Id)
                        {
                            return property;
                        }
                    }
                    throw new InvalidOperationException($"Could not locate property {JsonPropertyNames.Id} on data object type {type.Name}, {dataObjectTypeName}");
                });

                if (targetProperty.GetValue(dataObject) == null) throw new InvalidOperationException($"Missing value for field {JsonPropertyNames.Id} on data object type {typeof(T).Name}, {dataObjectTypeName}");
            }
        }

        private string GetPartitionKeyValue<T>(string dataObjectTypeName, T dataObject)
        {
            var partitionKeyName = _configuration[ConfigKeys.DataPartitionKey(_layer, dataObjectTypeName)];

            if (dataObject is JObject jDataObject)
            {
                return jDataObject.Value<string>(partitionKeyName) ?? throw new InvalidOperationException($"Unexpected null {partitionKeyName} (partition key field) value in {nameof(dataObject)}");
            }

            PropertyInfo targetProperty = partitionKeyPropertyCache.GetOrAdd(typeof(T), (type) =>
            {
                foreach (PropertyInfo property in type.GetProperties())
                {
                    var jsonProperty = property.GetCustomAttribute<JsonPropertyAttribute>();
                    if (jsonProperty != null && jsonProperty.PropertyName == partitionKeyName)
                    {
                        return property;
                    }
                }
                throw new InvalidOperationException($"Could not locate property {partitionKeyName} on data object type {type.Name}, {dataObjectTypeName}");
            });

            return targetProperty.GetValue(dataObject)?.ToString() ?? throw new InvalidOperationException($"Missing partition key value for field {partitionKeyName}");
        }

    }
}
