using System.Text.Encodings.Web;
using GovUK.Dfe.FlexForms.Web.Telemetry;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Telemetry;

public class TenantJavaScriptSnippetTests
{
    private const string InstrumentationKey = "11111111-1111-1111-1111-111111111111";
    private const string ValidConnectionString =
        $"InstrumentationKey={InstrumentationKey};IngestionEndpoint=https://uksouth-1.in.applicationinsights.azure.com/";
    private const string NonceItemKey = "NETESCAPADES_NONCE";

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void FullScript_ShouldBeEmpty_WhenConnectionStringMissing(string? connectionString)
    {
        var snippet = CreateSnippet(connectionString, new DefaultHttpContext());

        Assert.Equal(string.Empty, snippet.FullScript);
    }

    [Fact]
    public void FullScript_ShouldBeEmpty_WhenConnectionStringHasNoInstrumentationKey()
    {
        var snippet = CreateSnippet("IngestionEndpoint=https://example.test/", new DefaultHttpContext());

        Assert.Equal(string.Empty, snippet.FullScript);
    }

    [Fact]
    public void FullScript_ShouldBeEmpty_WhenConnectionStringIsMalformed()
    {
        var snippet = CreateSnippet($"InstrumentationKey={InstrumentationKey};not-a-key-value-pair", new DefaultHttpContext());

        Assert.Equal(string.Empty, snippet.FullScript);
    }

    [Fact]
    public void FullScript_ShouldAddNonceToScriptTags_WhenRequestHasNonce()
    {
        var context = new DefaultHttpContext();
        context.Items[NonceItemKey] = "abc123";

        var script = CreateSnippet(ValidConnectionString, context).FullScript;

        Assert.Contains(InstrumentationKey, script);
        Assert.Contains("<script nonce=\"abc123\"", script);
        Assert.DoesNotContain("<script>", script);
    }

    [Fact]
    public void FullScript_ShouldRenderWithoutNonce_WhenThereIsNoHttpContext()
    {
        var script = CreateSnippet(ValidConnectionString, httpContext: null).FullScript;

        Assert.Contains(InstrumentationKey, script);
        Assert.Contains("<script", script);
        Assert.DoesNotContain("nonce=", script);
    }

    private static TenantJavaScriptSnippet CreateSnippet(string? connectionString, HttpContext? httpContext)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [TenantApplicationInsightsConnection.ConfigurationKey] = connectionString
            })
            .Build();

        var appConfig = Substitute.For<ITenantAppConfiguration>();
        appConfig.Current.Returns(configuration);

        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        return new TenantJavaScriptSnippet(
            appConfig,
            Options.Create(new ApplicationInsightsServiceOptions()),
            httpContextAccessor,
            JavaScriptEncoder.Default);
    }
}
