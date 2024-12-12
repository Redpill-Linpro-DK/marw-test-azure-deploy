using Newtonsoft.Json;

namespace DIH.Common.Json.DeserializerAppendVisitors
{
    /// <summary>
    /// Implement to add a visitor to JSON deserialization - this enable you to append/overwrite properties on objects deserialized with <see cref="LazyJsonStreamDeserializer"/>
    /// </summary>
    public interface IDeserializerAppendVisitor
    {
        /// <summary>
        /// Called when a new data object is about to be deserialized
        /// </summary>
        /// <param name="sourceJsonReader">
        /// The JsonTextReader as it is at the beginning of the object read. 
        /// Use this if you need to know the path or depth at 'object start' 
        /// </param>
        void OnNewObject(JsonTextReader sourceJsonReader);

        /// <summary>
        /// Called for every token while the object is being deserialized
        /// </summary>
        /// <param name="sourceJsonReader">
        /// Use this if you need to know the current token 
        /// </param>
        void OnToken(JsonTextReader sourceJsonReader);

        /// <summary>
        /// Called before object close.
        /// Use this to append/overwrite properties.
        /// </summary>
        /// <param name="objectJsonWriter">The JsonTextWriter you should write your property and value to</param>
        /// <param name="objectAsStringFunc">The JSON for the object as it look at this point</param>
        void OnFinalizeObject(JsonTextWriter objectJsonWriter, Func<string> objectAsStringFunc);

        void OnBeforeReturnObject(object deserialized);
    }
}

