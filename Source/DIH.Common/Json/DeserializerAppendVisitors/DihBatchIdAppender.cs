using Newtonsoft.Json;

namespace DIH.Common.Json.DeserializerAppendVisitors
{
    /// <summary>
    /// Appends the provided <see cref="JsonPropertyNames.DIH_UpdatingBatchId"/> to the deserialized object
    /// </summary>
    public class DihBatchIdAppender : IDeserializerAppendVisitor
    {
        private readonly string _batchId;
        public DihBatchIdAppender(string batchId) => _batchId = batchId;

        public void OnNewObject(JsonTextReader sourceJsonReader) { }

        public void OnToken(JsonTextReader sourceJsonReader) { }

        public void OnFinalizeObject(JsonTextWriter objectJsonWriter, Func<string> objectAsStringFunc)
        {
            objectJsonWriter.WritePropertyName(JsonPropertyNames.DIH_UpdatingBatchId);
            objectJsonWriter.WriteValue(_batchId);
        }

        public void OnBeforeReturnObject(object deserialized) { }
    }
}

