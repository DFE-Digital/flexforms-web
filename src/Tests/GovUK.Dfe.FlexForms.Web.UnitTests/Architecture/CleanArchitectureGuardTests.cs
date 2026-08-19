using GovUK.Dfe.CoreLibs.Testing.Architecture;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Domain.FormEngine;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Architecture;

public class CleanArchitectureGuardTests
{
    private const string DomainNs = "GovUK.Dfe.FlexForms.Domain";
    private const string ApplicationNs = "GovUK.Dfe.FlexForms.Application";
    private const string InfrastructureNs = "GovUK.Dfe.FlexForms.Infrastructure";
    private const string WebNs = "GovUK.Dfe.FlexForms.Web";

    [Fact]
    public void PageModels_ShouldNotDependOnInfrastructure()
    {
        var failures = CleanArchitectureGuard.AssertNamespaceDoesNotDependOn(
            "GovUK.Dfe.FlexForms.Web.Pages", InfrastructureNs);

        Assert.Empty(failures);
    }

    [Fact]
    public void Application_ShouldNotDependOnInfrastructureOrWeb()
    {
        var failures = CleanArchitectureGuard.AssertNoForbiddenDependencies(
            typeof(IPrepareFormEngineGet).Assembly, InfrastructureNs, WebNs);

        Assert.Empty(failures);
    }

    [Fact]
    public void Application_ShouldNotTakeAspNetCoreSessionOrModelState()
    {
        var failures = CleanArchitectureGuard.AssertNoForbiddenDependencies(
            typeof(IPrepareFormEngineGet).Assembly,
            "Microsoft.AspNetCore.Http",
            "Microsoft.AspNetCore.Mvc");

        Assert.Empty(failures);
    }

    [Fact]
    public void Domain_ShouldNotDependOnOuterLayers()
    {
        var failures = CleanArchitectureGuard.AssertNoForbiddenDependencies(
            typeof(CheckboxValueNormalizer).Assembly, ApplicationNs, InfrastructureNs, WebNs);

        Assert.Empty(failures);
    }

    [Fact]
    public void ValidateCleanLayers_ShouldPassForAllLayers()
    {
        var violations = CleanArchitectureGuard.ValidateCleanLayers(
            typeof(CheckboxValueNormalizer).Assembly,
            typeof(IPrepareFormEngineGet).Assembly,
            DomainNs, ApplicationNs, InfrastructureNs, WebNs);

        Assert.Empty(violations);
    }
}
