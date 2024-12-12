using Azure.Security.KeyVault.Secrets;
using DIH.Common.Credential;

namespace DIH.Common.Services.Secrets
{
    public class AzureKeyvaultSecretsService : ISecretsService
    {
        private readonly SecretClient _secretClient;

        public AzureKeyvaultSecretsService(string keyVaultName)
        {
            // Construct the Key Vault URI
            var keyVaultUri = new Uri($"https://{keyVaultName}.vault.azure.net/");

            // Use DefaultAzureCredential which includes managed identity and other authentication methods
            var credential = AzureCredentialFactory.BuildDefault();

            // Initialize the SecretClient
            _secretClient = new SecretClient(keyVaultUri, credential);
        }

        public string GetSecret(string secretName)
        {
            // Retrieve the secret value
            KeyVaultSecret secret = _secretClient.GetSecret(secretName);
            return secret.Value;
        }
    }
}
