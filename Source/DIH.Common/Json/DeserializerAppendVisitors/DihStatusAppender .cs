using Newtonsoft.Json;

namespace DIH.Common.Json.DeserializerAppendVisitors
{
    /// <summary>
    /// Appends the provided <see cref="JsonPropertyNames.DIH_UpdatingStatus"/> to the deserialized object
    /// </summary>
    public class DihStatusAppender : IDeserializerAppendVisitor
    {
        private readonly string _status;
        public DihStatusAppender(string status) => _status = status;

        public void OnNewObject(JsonTextReader sourceJsonReader) { }

        public void OnToken(JsonTextReader sourceJsonReader) { }

        public void OnFinalizeObject(JsonTextWriter objectJsonWriter, Func<string> objectAsStringFunc)
        {
            objectJsonWriter.WritePropertyName(JsonPropertyNames.DIH_Status);
            objectJsonWriter.WriteValue(_status);
        }

        public void OnBeforeReturnObject(object deserialized) { }
    }
}

