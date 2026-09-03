using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;

namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Registers Data Protection so session and cookie-auth cookies can be unprotected on any replica.
/// Same Azure Blob + Key Vault pattern as the API; use a distinct ApplicationName and blob path.
/// </summary>
public static class SharedDataProtectionExtensions
{
    public static IDataProtectionBuilder AddSharedDataProtection(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var settings = configuration
            .GetSection(DataProtectionSettings.SectionName)
            .Get<DataProtectionSettings>()
            ?? new DataProtectionSettings();

        var builder = services.AddDataProtection();

        if (ShouldUseLocalKeyRing(environment, settings))
            return builder;

        var applicationName = string.IsNullOrWhiteSpace(settings.ApplicationName)
            ? "GovUK.Dfe.FlexForms.Web"
            : settings.ApplicationName;

        builder.SetApplicationName(applicationName);

        if (string.IsNullOrWhiteSpace(settings.BlobUri))
        {
            throw new InvalidOperationException(
                "DataProtection:BlobUri is required when DataProtection:UseAzure is true.");
        }

        if (string.IsNullOrWhiteSpace(settings.KeyVaultKeyId))
        {
            throw new InvalidOperationException(
                "DataProtection:KeyVaultKeyId is required when DataProtection:UseAzure is true.");
        }

        if (!Uri.TryCreate(settings.BlobUri, UriKind.Absolute, out var blobUri))
        {
            throw new InvalidOperationException(
                "DataProtection:BlobUri must be an absolute URI.");
        }

        if (!Uri.TryCreate(settings.KeyVaultKeyId, UriKind.Absolute, out var keyVaultKeyUri))
        {
            throw new InvalidOperationException(
                "DataProtection:KeyVaultKeyId must be an absolute URI.");
        }

        var credential = CreateKeyVaultCredential(environment, settings);

        if (settings.UseStorageSas)
        {
            if (string.IsNullOrWhiteSpace(blobUri.Query) || blobUri.Query.Length <= 1)
            {
                throw new InvalidOperationException(
                    "DataProtection:BlobUri must include a SAS query string when DataProtection:UseStorageSas is true. " +
                    "Example: https://account.blob.core.windows.net/container/web-keys.xml?sp=rw&st=...&sig=...");
            }

            builder.PersistKeysToAzureBlobStorage(blobUri);
        }
        else
        {
            builder.PersistKeysToAzureBlobStorage(blobUri, credential);
        }

        return builder.ProtectKeysWithAzureKeyVault(keyVaultKeyUri, credential);
    }

    private static DefaultAzureCredential CreateKeyVaultCredential(
        IHostEnvironment environment,
        DataProtectionSettings settings)
    {
        var useDeveloperCredentials =
            settings.UseStorageSas || environment.IsEnvironment("Local") || environment.IsDevelopment();

        if (!useDeveloperCredentials)
            return new DefaultAzureCredential();

        return new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ExcludeManagedIdentityCredential = true,
            ExcludeWorkloadIdentityCredential = true,
            ExcludeEnvironmentCredential = false,
            ExcludeAzureCliCredential = false,
            ExcludeVisualStudioCredential = false,
            ExcludeAzurePowerShellCredential = false,
            ExcludeInteractiveBrowserCredential = true
        });
    }

    private static bool ShouldUseLocalKeyRing(
        IHostEnvironment environment,
        DataProtectionSettings settings)
    {
        if (!settings.UseAzure)
            return true;

        // Launch profiles use Development; Local is also used. Keep the local key ring unless
        // the developer opts into Azure via UseStorageSas.
        if ((environment.IsDevelopment() || environment.IsEnvironment("Local")) && !settings.UseStorageSas)
            return true;

        return false;
    }
}
