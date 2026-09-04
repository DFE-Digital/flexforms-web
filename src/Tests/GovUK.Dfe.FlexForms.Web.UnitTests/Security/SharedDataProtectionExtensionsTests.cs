using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public class SharedDataProtectionExtensionsTests
{
    [Theory]
    [InlineData("Local")]
    [InlineData("Development")]
    public void AddSharedDataProtection_LocalOrDevelopment_UsesLocalKeysWhenAzureNotConfigured(string environmentName)
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            useAzure: true,
            useStorageSas: false,
            blobUri: "",
            keyVaultKeyId: "");
        var environment = new TestHostEnvironment(environmentName);

        var builder = services.AddSharedDataProtection(configuration, environment);

        Assert.NotNull(builder);
        using var provider = services.BuildServiceProvider();
        var dataProtection = provider.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtection.CreateProtector("FlexForms.Web.Cookies.v1");
        var cipher = protector.Protect("hello");
        Assert.Equal("hello", protector.Unprotect(cipher));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Local")]
    public void AddSharedDataProtection_LocalOrDevelopment_UsesAzureWhenFullyConfigured(string environmentName)
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            useAzure: true,
            useStorageSas: false,
            blobUri: "https://example.blob.core.windows.net/keys/web-keys.xml",
            keyVaultKeyId: "https://example.vault.azure.net/keys/k");
        var environment = new TestHostEnvironment(environmentName);

        var builder = services.AddSharedDataProtection(configuration, environment);

        Assert.NotNull(builder);
    }

    [Theory]
    [InlineData("Test")]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void AddSharedDataProtection_NonLocalWithUseAzureFalse_UsesLocalKeys(string environmentName)
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(useAzure: false, useStorageSas: false, blobUri: "", keyVaultKeyId: "");
        var environment = new TestHostEnvironment(environmentName);

        services.AddSharedDataProtection(configuration, environment);

        using var provider = services.BuildServiceProvider();
        var dataProtection = provider.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtection.CreateProtector("FlexForms.Web.Cookies.v1");
        var cipher = protector.Protect("hello");
        Assert.Equal("hello", protector.Unprotect(cipher));
    }

    [Fact]
    public void AddSharedDataProtection_TestEnvironmentWithUseAzureTrue_RequiresAzureConfig()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            useAzure: true,
            useStorageSas: false,
            blobUri: "",
            keyVaultKeyId: "https://example.vault.azure.net/keys/k");
        var environment = new TestHostEnvironment("Test");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSharedDataProtection(configuration, environment));

        Assert.Contains("BlobUri", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddSharedDataProtection_UseAzureTrueMissingBlobUri_Throws()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            useAzure: true,
            useStorageSas: false,
            blobUri: "",
            keyVaultKeyId: "https://example.vault.azure.net/keys/k");
        var environment = new TestHostEnvironment("Production");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSharedDataProtection(configuration, environment));

        Assert.Contains("BlobUri", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddSharedDataProtection_UseAzureFalseInProduction_UsesLocalKeys()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(useAzure: false, useStorageSas: false, blobUri: "", keyVaultKeyId: "");
        var environment = new TestHostEnvironment("Production");

        services.AddSharedDataProtection(configuration, environment);
        using var provider = services.BuildServiceProvider();
        var dataProtection = provider.GetRequiredService<IDataProtectionProvider>();
        Assert.NotNull(dataProtection.CreateProtector("FlexForms.Web.Cookies.v1"));
    }

    [Fact]
    public void AddSharedDataProtection_UseAzureTrueMissingKeyVaultKeyId_Throws()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            useAzure: true,
            useStorageSas: false,
            blobUri: "https://example.blob.core.windows.net/keys/web-keys.xml",
            keyVaultKeyId: "");
        var environment = new TestHostEnvironment("Production");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSharedDataProtection(configuration, environment));

        Assert.Contains("KeyVaultKeyId", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddSharedDataProtection_UseStorageSasWithoutQueryString_Throws()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            useAzure: true,
            useStorageSas: true,
            blobUri: "https://example.blob.core.windows.net/keys/web-keys.xml",
            keyVaultKeyId: "https://example.vault.azure.net/keys/k");
        var environment = new TestHostEnvironment("Production");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSharedDataProtection(configuration, environment));

        Assert.Contains("SAS", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddSharedDataProtection_DevelopmentWithUseStorageSasMissingSas_Throws()
    {
        var services = new ServiceCollection();
        var configuration = BuildConfiguration(
            useAzure: true,
            useStorageSas: true,
            blobUri: "https://example.blob.core.windows.net/keys/web-keys.xml",
            keyVaultKeyId: "https://example.vault.azure.net/keys/k");
        var environment = new TestHostEnvironment("Development");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            services.AddSharedDataProtection(configuration, environment));

        Assert.Contains("SAS", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration BuildConfiguration(
        bool useAzure,
        bool useStorageSas,
        string blobUri,
        string keyVaultKeyId) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DataProtection:UseAzure"] = useAzure.ToString(),
                ["DataProtection:UseStorageSas"] = useStorageSas.ToString(),
                ["DataProtection:ApplicationName"] = "GovUK.Dfe.FlexForms.Web.Tests",
                ["DataProtection:BlobUri"] = blobUri,
                ["DataProtection:KeyVaultKeyId"] = keyVaultKeyId
            })
            .Build();

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
