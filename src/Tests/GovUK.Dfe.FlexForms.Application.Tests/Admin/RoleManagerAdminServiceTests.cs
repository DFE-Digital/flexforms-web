using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class RoleManagerAdminServiceTests
{
    private readonly IRolesClient _roles = Substitute.For<IRolesClient>();
    private readonly RoleManagerAdminService _service;
    private readonly RoleManagerWorkState _state = new() { NewRoleName = "Caseworker" };

    public RoleManagerAdminServiceTests()
    {
        _service = new RoleManagerAdminService(_roles, NullLogger<RoleManagerAdminService>.Instance);
    }

    [Fact]
    public async Task CreateFromTemplateAsync_ShouldRedirect_WhenTemplateKeyIsMissing()
    {
        var result = await _service.CreateFromTemplateAsync(_state, "  ");

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(RoleManagerMessages.TemplateRequired, result.ErrorMessage);
        await _roles.DidNotReceive().CreateFromTemplateAsync(
            Arg.Any<CreateTenantRoleFromTemplateRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenameAsync_ShouldRedirect_WhenNameIsMissing()
    {
        var result = await _service.RenameAsync(_state, Guid.NewGuid(), " ");

        Assert.Equal(RoleManagerMessages.NameRequired, result.ErrorMessage);
        await _roles.DidNotReceive().RenameAsync(
            Arg.Any<Guid>(),
            Arg.Any<RenameTenantRoleRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ShouldRedirect_WhenApiSucceeds()
    {
        _roles.CreateAsync(Arg.Any<CreateTenantRoleRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TenantRoleDto { RoleId = Guid.NewGuid(), Name = "Caseworker" });

        var result = await _service.CreateAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(RoleManagerMessages.Created("Caseworker"), result.SuccessMessage);
        await _roles.Received(1).CreateAsync(
            Arg.Is<CreateTenantRoleRequest>(r => r.Name == "Caseworker"),
            Arg.Any<CancellationToken>());
    }
}
