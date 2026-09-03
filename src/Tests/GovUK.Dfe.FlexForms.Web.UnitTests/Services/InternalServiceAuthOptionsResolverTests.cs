using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class InternalServiceAuthOptionsResolverTests
{
    [Fact]
    public void Resolve_ShouldPreferTenantInternalServiceAuth_OverHost()
    {
        var hostConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalServiceAuth:SecretKey"] = "host-secret-key-32chars-minimum!!",
                ["InternalServiceAuth:Issuer"] = "host-issuer",
                ["InternalServiceAuth:Audience"] = "host-audience",
                ["InternalServiceAuth:Services:0:Email"] = "host@service.com",
                ["InternalServiceAuth:Services:0:ApiKey"] = "host-api-key"
            })
            .Build();

        var tenantConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalServiceAuth:SecretKey"] = "tenant-secret-key-32chars-minimum!",
                ["InternalServiceAuth:Issuer"] = "tenant-issuer",
                ["InternalServiceAuth:Audience"] = "tenant-audience",
                ["InternalServiceAuth:Services:0:Email"] = "tenant@service.com",
                ["InternalServiceAuth:Services:0:ApiKey"] = "tenant-api-key"
            })
            .Build();

        var tenantContext = Substitute.For<ITenantRequestContext>();
        tenantContext.TenantName.Returns("Transfers");
        tenantContext.TenantConfiguration.Returns(tenantConfig);

        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton(tenantContext);
        httpContext.RequestServices = services.BuildServiceProvider();

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var resolver = new InternalServiceAuthOptionsResolver(
            accessor,
            hostConfig,
            NullLogger<InternalServiceAuthOptionsResolver>.Instance);

        var options = resolver.Resolve();

        Assert.Equal("tenant-secret-key-32chars-minimum!", options.SecretKey);
        Assert.Equal("tenant-issuer", options.Issuer);
        Assert.Equal("tenant-audience", options.Audience);
        Assert.Equal("tenant@service.com", options.Services.Single().Email);
        Assert.Equal("tenant-api-key", options.Services.Single().ApiKey);
    }

    [Fact]
    public void Resolve_ShouldFallBackToHost_WhenTenantSectionMissing()
    {
        var hostConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalServiceAuth:SecretKey"] = "host-secret-key-32chars-minimum!!",
                ["InternalServiceAuth:Issuer"] = "host-issuer",
                ["InternalServiceAuth:Audience"] = "host-audience",
                ["InternalServiceAuth:Services:0:Email"] = "host@service.com",
                ["InternalServiceAuth:Services:0:ApiKey"] = "host-api-key"
            })
            .Build();

        var tenantContext = Substitute.For<ITenantRequestContext>();
        tenantContext.TenantConfiguration.Returns((IConfiguration?)null);

        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton(tenantContext);
        httpContext.RequestServices = services.BuildServiceProvider();

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var resolver = new InternalServiceAuthOptionsResolver(
            accessor,
            hostConfig,
            NullLogger<InternalServiceAuthOptionsResolver>.Instance);

        var options = resolver.Resolve();

        Assert.Equal("host-secret-key-32chars-minimum!!", options.SecretKey);
        Assert.Equal("host@service.com", options.Services.Single().Email);
    }

    [Fact]
    public void Resolve_ShouldFallBackToHost_WhenTenantContextIsEmptyPlaceholder()
    {
        var hostConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalServiceAuth:SecretKey"] = "host-secret-key-32chars-minimum!!",
                ["InternalServiceAuth:Issuer"] = "host-issuer",
                ["InternalServiceAuth:Audience"] = "host-audience"
            })
            .Build();

        // Scoped ITenantRequestContext exists even on health/static bypass paths.
        var tenantContext = Substitute.For<ITenantRequestContext>();
        tenantContext.TenantId.Returns((Guid?)null);
        tenantContext.TenantName.Returns((string?)null);
        tenantContext.TenantConfiguration.Returns((IConfiguration?)null);

        var httpContext = new DefaultHttpContext();
        var services = new ServiceCollection();
        services.AddSingleton(tenantContext);
        httpContext.RequestServices = services.BuildServiceProvider();

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(httpContext);

        var resolver = new InternalServiceAuthOptionsResolver(
            accessor,
            hostConfig,
            NullLogger<InternalServiceAuthOptionsResolver>.Instance);

        var options = resolver.Resolve();

        Assert.Equal("host-secret-key-32chars-minimum!!", options.SecretKey);
    }

    [Fact]
    public void Resolve_ShouldUseAmbientTenant_WhenHttpContextMissing()
    {
        var tenantConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InternalServiceAuth:SecretKey"] = "tenant-secret-key-32chars-minimum!",
                ["InternalServiceAuth:Issuer"] = "tenant-issuer",
                ["InternalServiceAuth:Audience"] = "tenant-audience",
                ["InternalServiceAuth:Services:0:Email"] = "tenant@service.com",
                ["InternalServiceAuth:Services:0:ApiKey"] = "tenant-api-key"
            })
            .Build();

        var tenantContext = Substitute.For<ITenantRequestContext>();
        tenantContext.TenantName.Returns("Transfers");
        tenantContext.TenantConfiguration.Returns(tenantConfig);

        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns((HttpContext?)null);

        var resolver = new InternalServiceAuthOptionsResolver(
            accessor,
            new ConfigurationBuilder().Build(),
            NullLogger<InternalServiceAuthOptionsResolver>.Instance);

        using (AmbientTenantRequestContext.Use(tenantContext))
        {
            var options = resolver.Resolve();
            Assert.Equal("tenant-secret-key-32chars-minimum!", options.SecretKey);
            Assert.Equal("tenant@service.com", options.Services.Single().Email);
        }
    }
}
