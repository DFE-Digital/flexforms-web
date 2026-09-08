namespace GovUK.Dfe.FlexForms.Web.Security;

/// <summary>
/// Configuration for ASP.NET Core Data Protection used to protect session and auth cookies
/// across Web replicas.
/// </summary>
public sealed class DataProtectionSettings
{
    public const string SectionName = "DataProtection";

    /// <summary>
    /// When true, persists the key ring to Azure Blob Storage and protects it with Azure Key Vault.
    /// When false, uses the default local key ring (typical for local development without Azure).
    /// </summary>
    public bool UseAzure { get; set; }

    /// <summary>
    /// When true (and <see cref="UseAzure"/> is true), authenticates to blob storage using the SAS
    /// query string embedded in <see cref="BlobUri"/> instead of managed identity.
    /// Key Vault wrapping still uses <see cref="Azure.Identity.DefaultAzureCredential"/>.
    /// </summary>
    public bool UseStorageSas { get; set; }

    /// <summary>
    /// Stable application name for the Data Protection key ring.
    /// Must differ from the API so cookie keys are not mixed with TenantSettings keys.
    /// Do not change after keys have been issued in an environment.
    /// </summary>
    public string ApplicationName { get; set; } = "GovUK.Dfe.FlexForms.Web";

    /// <summary>
    /// Full blob URI for the shared key-ring XML. Must be a <strong>different blob</strong> than the API
    /// (e.g. …/web-keys.xml, not …/api-keys.xml).
    /// With managed identity: https://account.blob.core.windows.net/container/web-keys.xml
    /// With <see cref="UseStorageSas"/>: same URI plus SAS query string.
    /// </summary>
    public string? BlobUri { get; set; }

    /// <summary>
    /// Key Vault key identifier used to wrap the Data Protection key ring
    /// (e.g. https://vault.vault.azure.net/keys/web-cookie-dp).
    /// </summary>
    public string? KeyVaultKeyId { get; set; }
}
