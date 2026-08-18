using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Web.Pages.FormEngine;
using NetArchTest.Rules;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Architecture;

public class CleanArchitectureGuardTests
{
    [Fact]
    public void PageModels_ShouldNotDependOnInfrastructure()
    {
        var result = Types.InNamespace("GovUK.Dfe.FlexForms.Web.Pages")
            .ShouldNot()
            .HaveDependencyOn("GovUK.Dfe.FlexForms.Infrastructure")
            .GetResult();

        Assert.True(
            result.IsSuccessful,
            "PageModels must not reference Infrastructure: " + Format(result.FailingTypeNames));
    }

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrWeb()
    {
        var application = typeof(IPrepareFormEngineGet).Assembly;

        var infrastructure = Types.InAssembly(application)
            .ShouldNot()
            .HaveDependencyOn("GovUK.Dfe.FlexForms.Infrastructure")
            .GetResult();

        var web = Types.InAssembly(application)
            .ShouldNot()
            .HaveDependencyOn("GovUK.Dfe.FlexForms.Web")
            .GetResult();

        Assert.True(
            infrastructure.IsSuccessful,
            "Application must not reference Infrastructure: " + Format(infrastructure.FailingTypeNames));
        Assert.True(
            web.IsSuccessful,
            "Application must not reference Web: " + Format(web.FailingTypeNames));
    }

    [Fact]
    public void Application_ShouldNotTakeAspNetCoreSessionOrModelState()
    {
        var application = typeof(IPrepareFormEngineGet).Assembly;

        var session = Types.InAssembly(application)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore.Http")
            .GetResult();

        var mvc = Types.InAssembly(application)
            .ShouldNot()
            .HaveDependencyOn("Microsoft.AspNetCore.Mvc")
            .GetResult();

        Assert.True(
            session.IsSuccessful,
            "Application must not take ISession/HttpContext: " + Format(session.FailingTypeNames));
        Assert.True(
            mvc.IsSuccessful,
            "Application must not take ModelState: " + Format(mvc.FailingTypeNames));
    }

    [Fact]
    public void Domain_ShouldNotDependOnOuterLayers()
    {
        var domain = typeof(CheckboxValueNormalizer).Assembly;

        var application = Types.InAssembly(domain)
            .ShouldNot()
            .HaveDependencyOn("GovUK.Dfe.FlexForms.Application")
            .GetResult();
        var infrastructure = Types.InAssembly(domain)
            .ShouldNot()
            .HaveDependencyOn("GovUK.Dfe.FlexForms.Infrastructure")
            .GetResult();
        var web = Types.InAssembly(domain)
            .ShouldNot()
            .HaveDependencyOn("GovUK.Dfe.FlexForms.Web")
            .GetResult();

        Assert.True(application.IsSuccessful, Format(application.FailingTypeNames));
        Assert.True(infrastructure.IsSuccessful, Format(infrastructure.FailingTypeNames));
        Assert.True(web.IsSuccessful, Format(web.FailingTypeNames));
    }

    private static string Format(IEnumerable<string>? names) =>
        names is null ? "(none)" : string.Join(", ", names);
}
