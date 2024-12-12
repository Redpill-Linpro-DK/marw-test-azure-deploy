using Newtonsoft.Json;
using System.Security.Cryptography;
using System.Text;

namespace DIH.Common.Json.DeserializerAppendVisitors
{
    /// <summary>
    /// Appends a <see cref="JsonPropertyNames.DIH_Hash"/> to the deserialized object - hash is calculated from the content of the object as it looks when the visitor runs.
    /// 
    /// Take care to place this visitor early in the list of visitors, before volatile fileds like import date/hash id etc. - otherwise you may see different hash values for each run.
    /// </summary>
    public class DihHashAppender : IDeserializerAppendVisitor
    {
        private bool _addDihHashPropertyLocal = true;

        public void OnNewObject(JsonTextReader sourceJsonReader) => _addDihHashPropertyLocal = true;

        public void OnToken(JsonTextReader sourceJsonReader)
        {
            if (sourceJsonReader.TokenType == JsonToken.PropertyName && sourceJsonReader.Value?.ToString() == JsonPropertyNames.DIH_Hash)
            {
                _addDihHashPropertyLocal = false;
            }

        }

        public void OnFinalizeObject(JsonTextWriter objectJsonWriter, Func<string> objectAsStringFunc)
        {
            if (_addDihHashPropertyLocal)
            {
                using (SHA256 sha256Hash = SHA256.Create())
                {
                    // ComputeHash - returns byte array
                    byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(objectAsStringFunc()));
                    objectJsonWriter.WritePropertyName(JsonPropertyNames.DIH_Hash);
                    objectJsonWriter.WriteValue(bytes);
                }
            }
        }

        public void OnBeforeReturnObject(object deserialized) { }
    }
}

