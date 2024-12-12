namespace DIH.Common.Services.Settings
{
    /// <summary>
    /// Provides runtime limits that functions should observe 
    /// in order to keep resource use at a safe level.
    /// </summary>
    public interface IFunctionsSettingsService : IAsyncDisposable
    {
        /// <summary>
        /// Maximum number of general tasks that should run at the same time.
        /// </summary>
        int MaxParallelTasks { get; }

        /// <summary>
        /// For resource intensive tasks, this represents the number of maximum tasks that should run at the same time.
        /// </summary>
        int MaxParallelResourceIntensiveTasks { get; }

        /// <summary>
        /// Maximum number of concurrent tasks calling an external web based API.
        /// </summary>
        int MaxParallelApiCalls { get; }

        /// <summary>
        /// Limit of concurrent data objects functions should keep in memory at the same time.
        /// </summary>
        int MaxInMemObjects { get; }

        /// <summary>
        /// The maximum number of tasks/operations that a single message should generate at the receiver.
        /// 
        /// Ex an array of IDs to be deleted should always be below this number.
        /// </summary>
        int MaxTasksPerMessage { get; }

        /// <summary>
        /// When true any exception in one message should cancel the entire batch
        /// </summary>
        bool CancelFullBatchOnException { get; }

        /// <summary>
        /// The maximum time a batch job is guaranteed to have to complete. 
        /// 
        /// Jobs older than this age is considered okay to abort/cancel.
        /// </summary>
        TimeSpan BatchTimeout { get; }

        /// <summary>
        /// The maximum time a message is guaranteed to have to live. 
        /// 
        /// Messages older than this age is considered okay to drop.
        /// </summary>
        TimeSpan MessageTTL { get; }
    }
}


