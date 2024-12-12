using Azure;
using Azure.Data.Tables;
using Newtonsoft.Json;

namespace DIH.Common.Services.Table
{
    public class ObjectTableEntity<T> : ITableEntity where T : new()
    {
        // String field that contains serialized JSON
        public string Serialized { get; set; }
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }

        // Default constructor is required for the Azure SDK when retrieving the entity
        public ObjectTableEntity()
        {
            PartitionKey = "";
            RowKey = "";
            Serialized = "";
            Timestamp = DateTimeOffset.Now;
        }

        // Constructor that accepts an IMessage object and serializes it
        public ObjectTableEntity(string partitionKey, string rowKey, T message)
        {
            PartitionKey = partitionKey;
            RowKey = rowKey;
            Serialized = JsonConvert.SerializeObject(message);
            Timestamp = DateTimeOffset.Now;
        }

        // Method to deserialize the Serialized field back into an IMessage object
        public T GetObject()
        {
            return JsonConvert.DeserializeObject<T>(Serialized) ?? throw new InvalidOperationException($"Cannot deserialize message {Serialized}");
        }
    }
}

