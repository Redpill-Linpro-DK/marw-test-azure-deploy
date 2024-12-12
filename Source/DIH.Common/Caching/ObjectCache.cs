using System.Collections.Concurrent;

namespace DIH.Common.Caching
{
    public class ObjectCache<T> : IDisposable
    {
        private Func<string, string, Task<T>> ObjectBuilderAsync { get; }
        private TimeSpan MaxCacheTime { get; }

        private ConcurrentDictionary<string, TimestampedEntry> _cache = new ConcurrentDictionary<string, TimestampedEntry>();
        private Timer _purgeTimer;
        private bool _disposed = false;

        public ObjectCache(Func<string, string, Task<T>> objectBuilderAsync, TimeSpan maxCacheTime)
        {
            ObjectBuilderAsync = objectBuilderAsync;
            MaxCacheTime = maxCacheTime;

            // Initialize the timer to purge expired entries every 30 seconds
            _purgeTimer = new Timer(PurgeExpiredEntries, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public async Task<T> Get(string id)
        {
            return await Get(id, id);
        }

        public async Task<T> Get(string id, string partitionKey)
        {
            string key = $"{id}:::{partitionKey}";
            if (_cache.TryGetValue(key, out TimestampedEntry? result))
            {
                result.LastUsed = DateTime.Now; // Update last used time
                return result.Value;
            }

            T value = await ObjectBuilderAsync(id, partitionKey);
            if (value != null)
            {
                result = new TimestampedEntry(value, MaxCacheTime);
                _cache.TryAdd(key, result);
            }
            return value;
        }

        private void PurgeExpiredEntries(object? state)
        {
            // Remove expired entries
            var expiredEntries = _cache.Where(kvp => kvp.Value.Expires <= DateTime.Now).ToList();
            foreach (var entry in expiredEntries)
            {
                _cache.TryRemove(entry.Key, out _);
            }

            // Evict least recently used entries if there are more than 5000
            if (_cache.Count > 5000)
            {
                var entriesToEvict = _cache.OrderBy(kvp => kvp.Value.LastUsed).Take(_cache.Count - 5000).ToList();
                foreach (var entry in entriesToEvict)
                {
                    _cache.TryRemove(entry.Key, out _);
                }
            }
        }

        private class TimestampedEntry
        {
            public DateTime Expires { get; set; }
            public DateTime LastUsed { get; set; }
            public T Value { get; set; }

            public TimestampedEntry(T value, TimeSpan ttl)
            {
                Value = value;
                Expires = DateTime.Now + ttl;
                LastUsed = DateTime.Now; // Initialize last used time
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed state (managed objects)
                    _purgeTimer?.Dispose();
                }

                _disposed = true;
            }
        }
    }
}
