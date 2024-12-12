using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Identity;
using DIH.Common.Credential;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace DIH.Common.Configuration
{
    public static class ConfigurationHelper
    {
        public static void UseDihConfiguration(this IFunctionsConfigurationBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder, nameof(builder));

            // Get Azure App Configuration service URL, from local Function app config
            string appConfigEndpoint =
                Environment.GetEnvironmentVariable(EnvironmentConfigKeys.AzureAppConfigurationEndpoint) ?? "";
            if (string.IsNullOrEmpty(appConfigEndpoint))
                throw new InvalidOperationException(
                    $"{EnvironmentConfigKeys.AzureAppConfigurationEndpoint} environment variable / app local config not set");

            builder.ConfigurationBuilder.AddAzureAppConfiguration(options =>
            {
                options.Connect(new Uri(appConfigEndpoint), AzureCredentialFactory.BuildDefault())
                    .ConfigureRefresh(refreshOptions =>
                    {
                        // Register the sentinel key for refresh
                        refreshOptions.Register(ConfigKeys.DIH_Config_SentinelKey, refreshAll: true)
                            // Set cache expiration for sentinal check to minimize num of transactions
                            .SetCacheExpiration(TimeSpan.FromMinutes(5));
                    });
            });
        }
        public static void UseGlobalKeyVault(this IFunctionsConfigurationBuilder builder)
        {
            // Build the current configuration to access the Key Vault URL from Azure App Configuration
            var configuration = builder.ConfigurationBuilder.Build();

            // Retrieve Key Vault URL from Azure App Configuration
            string keyVaultUrl = configuration[ConfigKeys.DIH_GlobalKeyVault_Uri];
            if (!string.IsNullOrEmpty(keyVaultUrl))
            {
                var azureKeyVaultConfigurationOptions = new AzureKeyVaultConfigurationOptions
                {
                    ReloadInterval = TimeSpan.FromMinutes(5)
                };
                builder.ConfigurationBuilder.AddAzureKeyVault(new Uri(keyVaultUrl), new DefaultAzureCredential(),
                    azureKeyVaultConfigurationOptions);
            }
        }
        
        public static void UseLocalKeyVault(this IFunctionsConfigurationBuilder builder)
        {
           // Retrieve Key Vault URL from local Function app environment variable
            string keyVaultUrl = Environment.GetEnvironmentVariable(EnvironmentConfigKeys.LocalKeyVaultUri) ?? "";
            if (!string.IsNullOrEmpty(keyVaultUrl))
            {
                var azureKeyVaultConfigurationOptions = new AzureKeyVaultConfigurationOptions
                {
                    ReloadInterval = TimeSpan.FromMinutes(5)
                };
                builder.ConfigurationBuilder.AddAzureKeyVault(new Uri(keyVaultUrl), new DefaultAzureCredential(),
                    azureKeyVaultConfigurationOptions);
            }
        }
    }
}