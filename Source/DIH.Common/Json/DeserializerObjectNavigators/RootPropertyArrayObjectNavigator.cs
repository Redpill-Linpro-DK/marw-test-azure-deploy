using Newtonsoft.Json;

namespace DIH.Common.Json.DeserializerObjectNavigators
{
    public class RootPropertyArrayObjectNavigator : IDeserializerObjectNavigator
    {
        private bool _locatedArray = false;
        private int _arrayDepth;
        private int _objectDepth;

        private void GotoArrayStart(JsonTextReader jsonReader)
        {
            jsonReader.Read();
            if (jsonReader.TokenType == JsonToken.StartObject)
            {
                jsonReader.Read();
                if (jsonReader.TokenType == JsonToken.PropertyName)
                {
                    jsonReader.Read();
                    if (jsonReader.TokenType == JsonToken.StartArray)
                    {
                        _arrayDepth = jsonReader.Depth;
                        _locatedArray = true;
                    }
                }
            }
            if (!_locatedArray)
            {
                throw new InvalidDataException("JSON document should be formatted like {prop:[...]}.");
            }
        }

        public bool GotoNextObject(JsonTextReader jsonReader)
        {
            if (!_locatedArray) GotoArrayStart(jsonReader);
            jsonReader.Read();
            _objectDepth = jsonReader.Depth;
            return jsonReader.Depth > _arrayDepth && jsonReader.TokenType == JsonToken.StartObject;
        }

        public bool GotoNextObjectToken(JsonTextReader jsonReader)
        {
            jsonReader.Read();
            return jsonReader.Depth > _objectDepth;
        }
    }
}

