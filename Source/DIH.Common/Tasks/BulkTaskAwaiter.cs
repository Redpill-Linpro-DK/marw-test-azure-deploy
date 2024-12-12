namespace DIH.Common.Tasks
{
    /// <summary>
    /// Provide parallel task execution services - within healthy limiys
    /// </summary>
    public class BulkTaskAwaiter : IAsyncDisposable
    {
        private readonly int _awaitAtMaxTasks;
        private readonly List<Task> _tasks = new();
#if WINDOWS
    private readonly int _maxPercentProcessorLoad;
    private readonly PerformanceCounter _cpuCounter;
    private readonly TimeSpan _maxCpuCoolWait;
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="BulkTaskAwaiter"/> class, allowing up to N tasks to run concurrently before waiting for completion.
        /// </summary>
        /// <param name="awaitAtMaxTasks">The maximum number of uncompleted tasks allowed before automatic await. Too high a number may deplete resources, such as network connections.</param>
        /// <param name="maxProcessorLoadPct">The maximum percentage of processor load allowed before automatic await.</param>
        /// <param name="maxCpuCoolWaitSec">The maximum time to wait for the CPU to cool down, in seconds.</param>
        public BulkTaskAwaiter(int awaitAtMaxTasks = 25, int maxProcessorLoadPct = 100, int maxCpuCoolWaitSec = 0)
        {
            _awaitAtMaxTasks = awaitAtMaxTasks;
#if WINDOWS
        _maxPercentProcessorLoad = maxProcessorLoadPct;
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _maxCpuCoolWait = TimeSpan.FromSeconds(maxCpuCoolWaitSec);
#endif
        }

        /// <summary>
        /// Adds a task to the task list. When N tasks are uncompleted, the oldest task will be awaited before control is returned to the caller.
        /// </summary>
        /// <param name="t">The task to add.</param>
        public async Task Add(Task t)
        {
            _tasks.Add(t);
            ThrowOnTaskExceptions();
#if WINDOWS
        DateTime waitStart = DateTime.Now;
        while (_maxPercentProcessorLoad < 100 && _maxPercentProcessorLoad < _cpuCounter.NextValue() && DateTime.Now - waitStart < _maxCpuCoolWait)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }
#endif

            while (_tasks.Where(t => !t.IsCompleted).Count() >= _awaitAtMaxTasks)
            {
                List<Task> uncompletedTasks = _tasks.Where(t => !t.IsCompleted).ToList();
                if (uncompletedTasks.Count > 0) await Task.WhenAny(uncompletedTasks);
            }
        }

        /// <summary>
        /// Ensures that all added tasks have completed.
        /// </summary>
        public async Task AwaitAll()
        {
            await Task.WhenAll(_tasks);
            ThrowOnTaskExceptions();
        }

        /// <summary>
        /// Ensures that all added tasks have completed and return the results
        /// </summary>
        public async Task<List<T>> AwaitAll<T>()
        {
            await Task.WhenAll(_tasks);
            ThrowOnTaskExceptions();
            return _tasks.Where(t => t.IsCompletedSuccessfully)
                         .Select(t => ((Task<T>)t).Result)
                         .ToList();
        }

        /// <summary>
        /// Asynchronously releases all resources used by the current instance of the <see cref="BulkTaskAwaiter"/> class.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            await AwaitAll();
#if WINDOWS
        _cpuCounter.Dispose();
#endif
        }

        /// <summary>
        /// Throws an exception if any of the tasks have failed.
        /// </summary>
        public void ThrowOnTaskExceptions()
        {
            if (_tasks.Any(t => t.IsFaulted))
            {
                IEnumerable<Exception> thrownExceptions = _tasks.Where(t => t.IsFaulted).Select(t => t.Exception ?? new Exception("No exception known..."));
                throw new AggregateException("One or more tasks have failed.", thrownExceptions);
            }
        }
    }
}