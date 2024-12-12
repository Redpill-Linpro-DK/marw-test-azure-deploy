using Newtonsoft.Json;

namespace DIH.Common.Json.DeserializerAppendVisitors
{
    /// <summary>
    /// Appends <see cref="JsonPropertyNames.DIH_LastUpdate"/> to the deserialized object
    /// </summary>
    public class DihImportTimeAppender : IDeserializerAppendVisitor
    {
        public void OnNewObject(JsonTextReader sourceJsonReader) { }

        public void OnToken(JsonTextReader sourceJsonReader) { }

        public void OnFinalizeObject(JsonTextWriter objectJsonWriter, Func<string> objectAsStringFunc)
        {
            objectJsonWriter.WritePropertyName(JsonPropertyNames.DIH_LastUpdate);
            objectJsonWriter.WriteValue(DateTime.Now);
        }

        public void OnBeforeReturnObject(object deserialized) { }
    }
}

