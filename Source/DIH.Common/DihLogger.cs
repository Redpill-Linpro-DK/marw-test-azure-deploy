using Microsoft.Extensions.Logging;
using System.Text;

namespace DIH.Common
{
    public static class LoggerExtensions
    {
        /// <summary>
        /// Wraps the given logger instance to provide DIH consistent logging.
        /// </summary>
        /// <typeparam name="T">The type used to mark log lines with sender.</typeparam>
        /// <param name="logger">The ILogger instance to wrap.</param>
        /// <returns>A DihLogger instance providing DIH consistent logging.</returns>
        public static DihLogger<T> AsDihLogger<T>(this ILogger<T> logger) => new(logger);
    }

    /// <summary>
    /// Common strings to use for sourceTyoe and targetType when calling DihMonitorSuccess and DihMonitorError on DihLogger<T>
    /// </summary>
    public static class DihMonitorSystemType
    {
        public static string DIH = "dih";
        public static string System = "system";
    }

    /// <summary>
    /// Common strings to use for sourceName and targetName when calling DihMonitorSuccess and DihMonitorError on DihLogger<T>
    /// </summary>
    public static class DihMonitorSystemName
    {
        public static string ExternalHttpClient = "http-client";
        public static string ExternalMessage = "message";
        public static string DihIngestionChange = "ingestion-change";
        public static string DihIngestionFullbatch = "ingestion-fullbatch";
        public static string DihIngestionOrchestration = "ingestion-orchestration";
        public static string DihRawChange = "raw-change";
        public static string DihRawFullbatchProcessing = "raw-fullbatch-processing";
        public static string DihPreparedChange = "prepared-change";
    }

    /// <summary>
    /// Provides a wrapper for ILogger to implement DIH consistent logging.
    /// </summary>
    /// <typeparam name="T">The type of the class using this logger. The class name is used to prefix log lines.</typeparam>
    public class DihLogger<T> : ILogger<T>
    {
        private readonly ILogger<T> _logger;
        private readonly string _lineStart;

        // Dih log lines are prefixed with calling class name - below is how many characters to spend on that
        private const int CLASSNAME_PREFIX_LENGTH = 40;

        /// <summary>
        /// Initializes a new instance of the DihLogger class.
        /// </summary>
        /// <param name="logger">The ILogger instance to wrap.</param>
        public DihLogger(ILogger<T> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Type sourceType = logger.GetType().IsGenericType ? logger.GetType().GetGenericArguments()[0] : throw new InvalidOperationException("Logger must be of generic type.");
            string append = $".{LabelNumber<T>.GetNextValue()}";
            int shortNameMaxLength = CLASSNAME_PREFIX_LENGTH - append.Length;
            string shortName = (sourceType.Name.Length <= shortNameMaxLength ? sourceType.Name : sourceType.Name.Substring(0, shortNameMaxLength));
            string label = $"{shortName}{append}";
            _lineStart = $"DIH:{label}{new string(' ', CLASSNAME_PREFIX_LENGTH - label.Length)}";
        }


        /// <summary>
        /// Logs a debug level message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void DihDebug(string message, string batchId = "") =>
            _logger.LogDebug(LogMessageGenerator($"{_lineStart}: {message}", batchId));

        /// <summary>
        /// Logs an informational level message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void DihInformation(string message, string batchId = "") =>
            _logger.LogInformation(LogMessageGenerator($"{_lineStart}: {message}", batchId));

        /// <summary>
        /// Logs a warning level message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void DihWarning(string message, string batchId = "") =>
            _logger.LogWarning(LogMessageGenerator($"{_lineStart}: {message}", batchId));

        /// <summary>
        /// Logs an error level message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void DihError(string message, string batchId = "") =>
            _logger.LogError(LogMessageGenerator($"{_lineStart}: {message}", batchId));

        /// <summary>
        /// Logs an error message and exception details.
        /// </summary>
        /// <param name="ex">The exception to log.</param>
        /// <param name="message">The message to log.</param>
        public void DihError(Exception ex, string message = "", string batchId = "")
        {
            if (!string.IsNullOrEmpty(message))
                _logger.LogError(LogMessageGenerator($"{_lineStart}: {message}", batchId));

            _logger.LogError(LogMessageGenerator($"{_lineStart}: {ex.Message}", batchId));
        }

        /// <summary>
        /// Logs a trace level message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        public void DihTrace(string message, string batchId = "") =>
            _logger.LogTrace(LogMessageGenerator($"{_lineStart}: {message}", batchId));

        /// <summary>
        /// Logs a transient event and the retry attempt info.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="nextRetryCount">The count of next retry attempt.</param>
        /// <param name="totalRetryCount">The total number of retry attempts.</param>
        /// <param name="retryDelayMs">The delay before next retry in milliseconds.</param>
        public void DihTransientEvent(string message, int nextRetryCount, int totalRetryCount, int retryDelayMs, string batchId = "")
        {
            int retryPercent = (int)(100 * ((decimal)nextRetryCount / totalRetryCount));
            message = $"Transient event: {message} (Next retry {nextRetryCount}/{totalRetryCount} in {retryDelayMs} ms)";
            if (retryPercent <= 20) DihDebug(message, batchId);
            else if (retryPercent <= 40) DihInformation(message, batchId);
            else if (retryPercent <= 75) DihWarning(message, batchId);
            else DihError(message, batchId);
        }

        /// <summary>
        /// Logs a successful operation in the DIH monitoring system with the specified details.
        /// </summary>
        /// <param name="sourceType">The type of the source system or component initiating the operation.</param>
        /// <param name="sourceName">The name of the source system or component initiating the operation.</param>
        /// <param name="targetType">The type of the target system or component involved in the operation.</param>
        /// <param name="targetName">The name of the target system or component involved in the operation.</param>
        /// <param name="message">An optional message describing the success of the operation. Defaults to "Success".</param>
        /// <param name="statusCode">An optional status code associated with the operation, typically representing an HTTP status code.</param>
        /// <param name="batchId">An optional batch ID associated with the operation, useful for tracking batched or grouped operations.</param>
        /// <param name="dataObjectTypeName">An optional data object type name involved in the operation, useful for categorizing or identifying the data being processed.</param>
        /// <param name="additionalProperties">An optional dictionary of additional properties to include in the log entry.</param>
        public void DihMonitorSuccess(string sourceType, string sourceName, string targetType, string targetName, string message = "Success", int? statusCode = null, string? batchId = null, string? dataObjectTypeName = null, IDictionary<string, object>? additionalProperties = null)
        {
            var properties = new Dictionary<string, object>
            {
                {"status", "success"}
            };

            // Merge additional properties if provided
            if (additionalProperties != null)
            {
                foreach (var prop in additionalProperties)
                {
                    properties[prop.Key] = prop.Value;
                }
            }

            this.DihMonitorLog(sourceType, sourceName, targetType, targetName, message, statusCode, batchId, dataObjectTypeName, properties);
        }

        /// <summary>
        /// Logs an error in the DIH monitoring system with the specified details.
        /// </summary>
        /// <param name="sourceType">The type of the source system or component where the error originated.</param>
        /// <param name="sourceName">The name of the source system or component where the error originated.</param>
        /// <param name="targetType">The type of the target system or component involved in the operation where the error occurred.</param>
        /// <param name="targetName">The name of the target system or component involved in the operation where the error occurred.</param>
        /// <param name="exception">An optional exception object that represents the error. This provides detailed information about the exception, including stack traces and error messages.</param>
        /// <param name="message">An optional message that describes the error or provides additional context. If not provided, a default message is generated based on the exception.</param>
        /// <param name="statusCode">An optional status code associated with the error, typically representing an HTTP status code.</param>
        /// <param name="batchId">An optional batch ID associated with the operation where the error occurred, useful for tracking batched or grouped operations.</param>
        /// <param name="dataObjectTypeName">An optional data object type name involved in the operation where the error occurred, useful for categorizing or identifying the data being processed.</param>
        /// <param name="additionalProperties">An optional dictionary of additional properties to include in the log entry.</param>
        public void DihMonitorError(string sourceType, string sourceName, string targetType, string targetName, Exception? exception = null, string? message = null, int? statusCode = null, string? batchId = null, string? dataObjectTypeName = null, IDictionary<string, object>? additionalProperties = null)
        {
            var properties = new Dictionary<string, object>
            {
                {"status", "error"}
            };

            if (exception != null)
            {
                properties.Add("exceptionMessage", exception.Message);
                if (exception.StackTrace != null) properties.Add("exceptionStackTrace", exception.StackTrace);
                if (exception.InnerException != null) properties.Add("exceptionInner", exception.InnerException.ToString());
                if (message == null) message = exception.Message;
            }

            if (message == null) message = "Error";

            // Merge additional properties if provided
            if (additionalProperties != null)
            {
                foreach (var prop in additionalProperties)
                {
                    properties[prop.Key] = prop.Value;
                }
            }

            this.DihMonitorLog(sourceType, sourceName, targetType, targetName, message, statusCode, batchId, dataObjectTypeName, properties);
        }

        private void DihMonitorLog(string sourceType, string sourceName, string targetType, string targetName, string message, int? statusCode, string? batchId, string? dataObjectTypeName, IDictionary<string, object> additionalProperties)
        {
            var properties = new Dictionary<string, object>
            {
                {"messageMonitor", "true"},
                {"identifier", "azFunctionApp"},
                {"processType", "azFunctionApp"},
                {"sourceType", sourceType},
                {"sourceName", sourceName},
                {"targetType", targetType},
                {"targetName", targetName},
                {"message", message}
            };

            if (statusCode != null) properties.Add("statusCode", statusCode);
            if (batchId != null) properties.Add("batchId", batchId);
            if (dataObjectTypeName != null) properties.Add("dataObjectTypeName", dataObjectTypeName);

            // Merge additional properties
            foreach (var prop in additionalProperties)
            {
                properties[prop.Key] = prop.Value;
            }

            using (_logger.BeginScope(properties))
            {
                if (properties["status"]?.ToString() == "success")
                {
                    _logger.LogInformation(message);
                }
                else
                {
                    _logger.LogError(message);
                }
            }
        }

        // Implementations for ILogger<T> interface:
        public IDisposable BeginScope<TState>(TState state) => _logger.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => _logger.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception, string> formatter) =>
            _logger.Log(logLevel, eventId, state, exception, formatter!);

        private static class LabelNumber<TT>
        {
            internal static int GetNextValue()
            {
                lock (typeof(LoggerExtensions))
                {
                    count++;
                    return count;
                }
            }

            private static int count = -1; // Initialize to -1 to start from 0 on the first call.
        }

        /// <summary>
        /// Generates a log message using a <see cref="StringBuilder"/> based on the given <paramref name="batchId"/>.
        /// </summary>
        private static string LogMessageGenerator(string startingString, string batchId)
        {
            StringBuilder builder = new(startingString);

            if (!string.IsNullOrWhiteSpace(batchId))
                builder.Append($" - BatchID: {batchId}");

            return builder.ToString();
        }
    }
}

