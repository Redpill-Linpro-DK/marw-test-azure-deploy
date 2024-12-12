using Azure.Identity;

namespace DIH.Common.Credential
{
    /// <summary>
    /// Provide Azure credentials
    /// </summary>
    public static class AzureCredentialFactory
    {
        /// <summary>
        /// Default Azure Credential - works on both development machines and on Azure
        /// </summary>
        /// <returns></returns>
        public static DefaultAzureCredential BuildDefault()
        {
            return new DefaultAzureCredential(new DefaultAzureCredentialOptions { ExcludeEnvironmentCredential = true });
        }
    }
}


