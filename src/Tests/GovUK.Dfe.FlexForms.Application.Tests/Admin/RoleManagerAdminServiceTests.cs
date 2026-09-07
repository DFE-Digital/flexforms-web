using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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

    [Fact]
    public async Task LoadAsync_ShouldPopulateRoles_WhenApiSucceeds()
    {
        _roles.ListAsync(Arg.Any<CancellationToken>()).Returns([
            new TenantRoleDto { RoleId = Guid.NewGuid(), Name = "Admin", IsSystem = true },
            new TenantRoleDto { RoleId = Guid.NewGuid(), Name = "Caseworker", IsSystem = false }
        ]);

        await _service.LoadAsync(_state);

        Assert.Equal(2, _state.Roles.Count);
        Assert.Equal("Admin", _state.Roles[0].Name);
        Assert.False(_state.HasError);
    }

    [Fact]
    public async Task LoadAsync_ShouldSetError_WhenApiFails()
    {
        _roles.ListAsync(Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 500, "err", null!, null!));

        await _service.LoadAsync(_state);

        Assert.True(_state.HasError);
        Assert.Equal(RoleManagerMessages.LoadFailed + " (HTTP 500)", _state.ErrorMessage);
        Assert.Empty(_state.Roles);
    }

    [Fact]
    public async Task CreateFromTemplateAsync_ShouldRedirect_WhenApiSucceeds()
    {
        _roles.CreateFromTemplateAsync(Arg.Any<CreateTenantRoleFromTemplateRequest>(), Arg.Any<CancellationToken>())
            .Returns(new TenantRoleDto { RoleId = Guid.NewGuid(), Name = "Reviewer" });

        var result = await _service.CreateFromTemplateAsync(_state, "reviewer-template");

        Assert.Equal(RoleManagerMessages.CreatedFromTemplate("Reviewer", "reviewer-template"), result.SuccessMessage);
    }

    [Fact]
    public async Task RenameAsync_ShouldRedirect_WhenApiSucceeds()
    {
        var roleId = Guid.NewGuid();

        var result = await _service.RenameAsync(_state, roleId, "Lead Caseworker");

        Assert.Equal(RoleManagerMessages.Renamed, result.SuccessMessage);
        await _roles.Received(1).RenameAsync(
            roleId,
            Arg.Is<RenameTenantRoleRequest>(r => r.Name == "Lead Caseworker"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ShouldRedirect_WhenApiSucceeds()
    {
        var roleId = Guid.NewGuid();

        var result = await _service.DeleteAsync(_state, roleId);

        Assert.Equal(RoleManagerMessages.Deleted, result.SuccessMessage);
        await _roles.Received(1).DeleteAsync(roleId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ShouldRedirectWithError_WhenApiFails()
    {
        var roleId = Guid.NewGuid();
        _roles.DeleteAsync(roleId, Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 403, "err", null!, null!));

        var result = await _service.DeleteAsync(_state, roleId);

        Assert.Equal(RoleManagerMessages.DeleteFailed + " (HTTP 403)", result.ErrorMessage);
    }
}
