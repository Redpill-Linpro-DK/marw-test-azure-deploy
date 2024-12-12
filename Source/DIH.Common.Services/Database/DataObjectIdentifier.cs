namespace DIH.Common.Services.Database
{
    public class DataObjectIdentifier
    {
        public string Id { get; set; }
        public string PartitionKey { get; set; }

        /// <summary>
        /// Identifies data where id is also partition key
        /// </summary>
        /// <param name="id">Data identifier. Also the partition key.</param>
        public DataObjectIdentifier(string id)
        {
            Id = id;
            PartitionKey = id;
        }

        /// <summary>
        /// Identifies data
        /// </summary>
        /// <param name="id"></param>
        /// <param name="partitionKey"></param>
        public DataObjectIdentifier(string id, string partitionKey)
        {
            Id = id;
            PartitionKey = partitionKey;
        }

        /// <summary>
        /// Creates an uninitialized DataObjectIdentifier
        /// </summary>
        public DataObjectIdentifier()
        {
            Id = "";
            PartitionKey = "";
        }

        public string ToKeyString()
        {
            return Id != PartitionKey ? $"{Id}:::{PartitionKey}" : Id;
        }

        public static DataObjectIdentifier FromKeyString(string keyString)
        {
            if (keyString.Contains(":::"))
            {
                var parts = keyString.Split(":::");
                return new DataObjectIdentifier(parts[0], parts[1]);
            }
            else
            {
                return new DataObjectIdentifier(keyString, keyString);
            }
        }
    }
}
