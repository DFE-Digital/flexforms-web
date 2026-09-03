using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class InternalAuthRequestCheckerTests
{
    [Fact]
    public void IsValidRequest_ShouldReturnTrue_WhenTenantCredentialsMatch()
    {
        var resolver = Substitute.For<IInternalServiceAuthOptionsResolver>();
        resolver.Resolve().Returns(new InternalServiceAuthOptions
        {
            Services =
            [
                new InternalServiceCredentials
                {
                    Email = "eat-transfer-service@service.com",
                    ApiKey = "tenant-a-key"
                }
            ]
        });

        var checker = new InternalAuthRequestChecker(
            resolver,
            NullLogger<InternalAuthRequestChecker>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers["x-service-email"] = "eat-transfer-service@service.com";
        context.Request.Headers["x-service-api-key"] = "tenant-a-key";

        Assert.True(checker.IsValidRequest(context));
    }

    [Fact]
    public void IsValidRequest_ShouldReturnFalse_WhenApiKeyIsForDifferentTenant()
    {
        var resolver = Substitute.For<IInternalServiceAuthOptionsResolver>();
        resolver.Resolve().Returns(new InternalServiceAuthOptions
        {
            Services =
            [
                new InternalServiceCredentials
                {
                    Email = "eat-transfer-service@service.com",
                    ApiKey = "tenant-a-key"
                }
            ]
        });

        var checker = new InternalAuthRequestChecker(
            resolver,
            NullLogger<InternalAuthRequestChecker>.Instance);

        var context = new DefaultHttpContext();
        context.Request.Headers["x-service-email"] = "eat-transfer-service@service.com";
        context.Request.Headers["x-service-api-key"] = "tenant-b-key";

        Assert.False(checker.IsValidRequest(context));
    }

    [Fact]
    public void IsValidRequest_ShouldNotResolveOptions_WhenServiceHeadersMissing()
    {
        var resolver = Substitute.For<IInternalServiceAuthOptionsResolver>();
        var checker = new InternalAuthRequestChecker(
            resolver,
            NullLogger<InternalAuthRequestChecker>.Instance);

        var context = new DefaultHttpContext();

        Assert.False(checker.IsValidRequest(context));
        resolver.DidNotReceive().Resolve();
    }
}
