using Microsoft.Extensions.Configuration;

namespace DIH.Common.Services.Settings
{
    public class ConfigBasedFunctionsSettingsService : IFunctionsSettingsService
    {
        private readonly IConfiguration _configuration;

        public ConfigBasedFunctionsSettingsService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        // Safe defaults - go higher via config
        public int MaxParallelTasks => Get(ConfigKeys.DIH_Functions_MaxParallelTasks, 100);
        public int MaxParallelResourceIntensiveTasks => Get(ConfigKeys.DIH_Functions_ResourceIntensive_MaxParallelTasks, 50);
        public int MaxParallelApiCalls => Get(ConfigKeys.DIH_Functions_ExternalWebApi_MaxParallelTasks, 16);
        public int MaxInMemObjects => Get(ConfigKeys.DIH_Functions_MaxInMemObjects, 500);
        public int MaxTasksPerMessage => Get(ConfigKeys.DIH_Functions_MaxTasksPerMessage, 500);
        public bool CancelFullBatchOnException => Get(ConfigKeys.DIH_Functions_CancelFullBatchOnException, false);
        public TimeSpan BatchTimeout => TimeSpan.FromSeconds(Get(ConfigKeys.DIH_Functions_BatchTimeoutSeconds, 3600));
        public TimeSpan MessageTTL => TimeSpan.FromSeconds(Get(ConfigKeys.DIH_Functions_MessageTTLSeconds, 36000));

        private int Get(string name, int defaultValue)
        {
            var config = _configuration[name];
            if (string.IsNullOrEmpty(config))
            {
                return defaultValue;
            }

            return int.TryParse(config, out int value) ? value : defaultValue;
        }

        private bool Get(string name, bool defaultValue)
        {
            var config = _configuration[name];
            if (string.IsNullOrEmpty(config))
            {
                return defaultValue;
            }

            return bool.TryParse(config, out bool value) ? value : defaultValue;
        }

        public ValueTask DisposeAsync()
        {
            // Nothing to dispose
            return ValueTask.CompletedTask;
        }
    }
}


