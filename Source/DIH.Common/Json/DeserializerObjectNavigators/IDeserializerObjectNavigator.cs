using Newtonsoft.Json;

namespace DIH.Common.Json.DeserializerObjectNavigators
{
    public interface IDeserializerObjectNavigator
    {
        bool GotoNextObject(JsonTextReader jsonReader);
        bool GotoNextObjectToken(JsonTextReader jsonReader);
    }
}

