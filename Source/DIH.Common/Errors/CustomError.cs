using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace DIH.Common.Errors
{
    public class CustomError
    {
        [JsonProperty("type")]
        public string Type { get; set; } = "about:blank";
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;
        [JsonProperty("status")]
        public int Status { get; set; } = 500;
        [JsonProperty("detail")]
        public string Detail { get; set; } = "Internal server error";

        public static ObjectResult AsObjectResult(Exception exception)
        {
            CustomError error = new();

            error.Status = exception is InvalidDataException || exception is InvalidOperationException ? 400 : 500;
            error.Title = exception.Source ?? "Exception";
            error.Detail = exception.InnerException != null ? $"{exception.Message} -> {exception.InnerException.Message}" : exception.Message;

            return new ObjectResult(error) { StatusCode = error.Status };
        }
    }
}

