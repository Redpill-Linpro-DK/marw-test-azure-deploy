using DIH.Common.Json.DeserializerAppendVisitors;
using DIH.Common.Json.DeserializerObjectNavigators;
using Newtonsoft.Json;
using System.Text;

namespace DIH.Common.Json
{
    /// <summary>
    /// Provides functionality to lazily deserialize JSON streams, allowing optional appending of metadata to the deserialized objects.
    /// </summary>
    public static class LazyJsonStreamDeserializer
    {
        /// <summary>
        /// Asynchronously converts a JSON document stream into a lazily-evaluated <see cref="IEnumerable{T}"/>.
        /// The input stream should represent a JSON document. The provided <see cref="IDeserializerObjectNavigator"/> dictate the format of the JSON.
        /// The provided list of <see cref="IDeserializerAppendVisitor"/> can append/overwrite properties during deserialization.
        /// </summary>
        /// <param name="stream">The stream containing the JSON document.</param>
        /// <param name="objectNavigator">IDeserializerObjectNavigator object that locate objects and properties in the JSON of the stream.</param>
        /// <param name="deserializerVisitors">List if IDeserializerVisitor to apply to deserialization.</param>
        /// <returns>An <see cref="IAsyncEnumerable{T}"/> that lazily deserializes and yields objects from the JSON stream.</returns>
        public static async IAsyncEnumerable<T> DeserializeJsonAsync<T>(this Stream stream, IDeserializerObjectNavigator objectNavigator, IEnumerable<IDeserializerAppendVisitor> deserializerVisitors)
        {
            var visitors = deserializerVisitors.ToList();

            using (StreamReader reader = new(stream))
            using (JsonTextReader jsonReader = new(reader))
            {
                StringBuilder sb = new();
                StringWriter strWriter = new(sb);
                JsonTextWriter jsonTextWriter = new(strWriter);

                while (objectNavigator.GotoNextObject(jsonReader))
                {
                    sb.Clear();
                    visitors.ForEach(v => v.OnNewObject(jsonReader));
                    jsonTextWriter.WriteToken(JsonToken.StartObject);

                    while (objectNavigator.GotoNextObjectToken(jsonReader))
                    {
                        visitors.ForEach(v => v.OnToken(jsonReader));
                        jsonTextWriter.WriteToken(jsonReader.TokenType, jsonReader.Value);
                    }

                    visitors.ForEach(v => v.OnFinalizeObject(jsonTextWriter, () => $"{sb}}}"));
                    jsonTextWriter.WriteToken(JsonToken.EndObject);

                    // Deserialize and yield the result asynchronously
                    yield return await Task.Run(() =>
                    {
                        T? deserialized = JsonConvert.DeserializeObject<T>(sb.ToString()) ?? throw new InvalidOperationException($"Failed to deserialize {sb}");
                        visitors.ForEach(v => v.OnBeforeReturnObject(deserialized));
                        return deserialized;
                    });
                }
            }
        }
    }
}

