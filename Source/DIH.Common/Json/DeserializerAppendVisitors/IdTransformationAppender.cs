using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace DIH.Common.Json.DeserializerAppendVisitors
{
    /// <summary>
    /// Appends an "id" attribute to the deserialized object, created from one or more property values in the JSON.
    /// 
    /// Specify paths to the desired field(s) in the constructor.
    /// </summary>
    public class IdTransformationAppender : IDeserializerAppendVisitor
    {
        private readonly IdBuilder _idBuilder;
        private readonly bool _moveIdToTop;

        /// <summary>
        /// Appends an "id" attribute to the deserialized object, created from one or more property values in the JSON.
        /// </summary>
        /// <param name="idTransformationPattern">Use . (dot) to index into sub-objects. Use + (plus) to use multiple fields. Examples: "product_id", "name.last_name+department.id" </param>
        /// <param name="moveIdToTop">When true the 'id' property will be moved to the top of properties</param>
        public IdTransformationAppender(string idTransformationPattern, bool moveIdToTop = true)
        {
            string fieldList = idTransformationPattern;
            IdBuilder.Transform transform = IdBuilder.Transform.AsString;

            if (!string.IsNullOrEmpty(idTransformationPattern) && idTransformationPattern.Contains(":"))
            {
                // Transformation (guid/hash) in effect
                var parts = idTransformationPattern.Split(':');
                if (parts.Length > 2) throw new ArgumentException($"Unexpected ID transformation pattern {idTransformationPattern} - a maximum of 1 ':' characters expected", nameof(idTransformationPattern));
                fieldList = parts[1];

                switch (parts[0].ToLower())
                {
                    case "guid":
                        transform = IdBuilder.Transform.AsGuid;
                        break;
                    case "hash":
                        transform = IdBuilder.Transform.AsHash;
                        break;
                    default:
                        throw new ArgumentException($"Unexpected transform rule {parts[0]} in ID transformation pattern {idTransformationPattern}", nameof(idTransformationPattern));
                }

            }
            string[]? idSubstitutes = !string.IsNullOrEmpty(fieldList) ? fieldList.Split('+') : null;

            _idBuilder = new IdBuilder(idSubstitutes, transform);
            _moveIdToTop = moveIdToTop;
        }

        public void OnNewObject(JsonTextReader sourceJsonReader) => _idBuilder.Reset(sourceJsonReader);

        public void OnToken(JsonTextReader sourceJsonReader) => _idBuilder.Check(sourceJsonReader);

        public void OnFinalizeObject(JsonTextWriter objectJsonWriter, Func<string> objectAsStringFunc) => _idBuilder.Write(objectJsonWriter);

        public void OnBeforeReturnObject(object deserialized)
        {
            if (_moveIdToTop && deserialized is JObject jObject)
            {
                JProperty? idProperty = jObject.Property(JsonPropertyNames.Id);

                if (idProperty != null)
                {
                    idProperty.Remove();
                    jObject.AddFirst(idProperty);
                }
            }
        }

        private class IdBuilder
        {
            private readonly bool _active;
            private readonly string[] _pathParts;
            private int _pathSkip = 0;
            private string[] _idParts;
            private Transform _idTransform;
            private int matchIdx = -1;

            public enum Transform
            {
                AsString,
                AsHash,
                AsGuid
            }

            /// <summary>
            /// Helper class that can build up an artificial ID value from 1 or more fields
            /// Construct with array of strings that define path to fields to be part of id.
            /// Example array ["location.id","person.last_name"] - . is used to index into sub-objects
            /// </summary>
            /// <param name="pathParts">Array of path strings or empty array for no ID building</param>
            internal IdBuilder(string[]? pathParts, Transform idTransform)
            {
                _active = pathParts != null && pathParts.Length > 0;
                _pathParts = pathParts ?? (Array.Empty<string>());
                _idParts = new string[_pathParts.Length];
                _idTransform = idTransform;
            }

            internal void Reset(JsonReader jsonReader)
            {
                if (!_active) return;
                _pathSkip = jsonReader.Path.Length + 1;
                _idParts = new string[_pathParts.Length];
                matchIdx = -1;
            }

            internal void Check(JsonReader jsonReader)
            {
                if (!_active) return;
                if (jsonReader.TokenType == JsonToken.PropertyName)
                {
                    string objPath = jsonReader.Path.Substring(_pathSkip);
                    matchIdx = Array.IndexOf(_pathParts, objPath);
                    return;
                }
                if (matchIdx > -1)
                {
                    _idParts[matchIdx] = jsonReader.Value?.ToString() ?? "";
                    matchIdx = -1;
                }
            }

            internal void Write(JsonTextWriter jsonTextWriter)
            {
                if (!_active) return;

                string idConcatinated = string.Join("", _idParts);
                jsonTextWriter.WritePropertyName(JsonPropertyNames.Id);

                if (_idTransform == Transform.AsString)
                {
                    jsonTextWriter.WriteValue(idConcatinated);
                }
                else
                {
                    using (MD5 md5 = MD5.Create())
                    {
                        byte[] hash = md5.ComputeHash(Encoding.Default.GetBytes(idConcatinated));

                        switch (_idTransform)
                        {
                            case Transform.AsHash:
                                jsonTextWriter.WriteValue(Base62Encode(hash));
                                break;
                            case Transform.AsGuid:
                                jsonTextWriter.WriteValue(new Guid(hash));
                                break;
                            default:
                                throw new InvalidOperationException($"Unhandled transformation {_idTransform}");
                        }
                    }
                }
            }

            /// <summary>
            /// Encodes a byte array to a base-62 string.
            /// </summary>
            /// <param name="bytes">The byte array to encode.</param>
            /// <returns>A base-62 encoded string, containing a-z, A-Z and 0-9</returns>
            public static string Base62Encode(byte[] bytes)
            {
                BigInteger bigInt = new BigInteger(bytes);
                if (bigInt == 0) return "0";

                var result = new List<char>();
                while (bigInt > 0)
                {
                    bigInt = BigInteger.DivRem(bigInt, 62, out BigInteger remainder);
                    result.Insert(0, Base62Chars[(int)remainder]);
                }

                return new string(result.ToArray());
            }
            private const string Base62Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        }
    }
}

