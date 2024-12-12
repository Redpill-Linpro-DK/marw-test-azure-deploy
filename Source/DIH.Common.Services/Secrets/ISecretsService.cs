namespace DIH.Common.Services.Secrets
{
    public interface ISecretsService
    {
        string GetSecret(string secretName);
    }
}
