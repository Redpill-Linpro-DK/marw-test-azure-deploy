using Newtonsoft.Json.Linq;

namespace DIH.Common.Json
{
    public static class ExtensionMethods
    {
        /// <summary>
        /// Returns the ID of the provided JSON object
        /// </summary>
        /// <param name="obj">Object to get ID from</param>
        /// <returns>The ID of the object</returns>
        public static string IdValue(this JObject obj)
        {
            string idValue = obj[JsonPropertyNames.Id]?.ToString() ?? throw new InvalidDataException($"Property '{JsonPropertyNames.Id}' not found on json object:\n{obj}");
            if (string.IsNullOrWhiteSpace(idValue)) throw new InvalidDataException($"Detected empty '{JsonPropertyNames.Id}' value on json object:\n{obj}");
            return idValue;
        }

        /// <summary>
        /// Returns the DIH Hash value of the provided JSON object
        /// </summary>
        /// <param name="obj">Object to get DIH Hash value from</param>
        /// <returns>The DIH Hash value of the object</returns>
        public static string HashValue(this JObject obj) =>
            obj[JsonPropertyNames.DIH_Hash]?.ToString() ?? throw new InvalidOperationException($"Property '{JsonPropertyNames.DIH_Hash} not found on json object:\n{obj}");
    }
}

