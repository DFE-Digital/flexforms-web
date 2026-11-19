using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Contributors;

namespace GovUK.Dfe.FlexForms.Application.Tests.Contributors;

public class ContributorAccessLabelsTests
{
    private static readonly Guid ApplicationId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    [Fact]
    public void ForApplication_MapsInviteCheckboxes_UsingSingular()
    {
        var contributor = new UserDto
        {
            Authorization = new UserAuthorizationDto
            {
                Roles = ["User"],
                Permissions =
                [
                    Permission(ResourceType.Application, AccessType.Read),
                    Permission(ResourceType.Application, AccessType.Write),
                    Permission(ResourceType.ApplicationFiles, AccessType.Read)
                ]
            }
        };

        var labels = ContributorAccessLabels.ForApplication(contributor, ApplicationId, "visit");

        Assert.Equal(
            ["View this visit", "Change this visit", "View uploaded files"],
            labels);
    }

    [Fact]
    public void ForApplication_IgnoresOtherApplicationsAndResourceTypes()
    {
        var otherApplicationId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
        var contributor = new UserDto
        {
            Authorization = new UserAuthorizationDto
            {
                Roles = ["User"],
                Permissions =
                [
                    Permission(ResourceType.Application, AccessType.Read),
                    new UserPermissionDto
                    {
                        ApplicationId = otherApplicationId,
                        ResourceType = ResourceType.Application,
                        AccessType = AccessType.Write,
                        ResourceKey = otherApplicationId.ToString()
                    },
                    new UserPermissionDto
                    {
                        ApplicationId = ApplicationId,
                        ResourceType = ResourceType.Notifications,
                        AccessType = AccessType.Read,
                        ResourceKey = "user@example.test"
                    }
                ]
            }
        };

        var labels = ContributorAccessLabels.ForApplication(contributor, ApplicationId, "application");

        Assert.Equal(["View this application"], labels);
    }

    [Fact]
    public void ForApplication_WhenAuthorizationMissing_ReturnsEmpty()
    {
        Assert.Empty(ContributorAccessLabels.ForApplication(new UserDto(), ApplicationId, "application"));
    }

    private static UserPermissionDto Permission(ResourceType resourceType, AccessType accessType) =>
        new()
        {
            ApplicationId = ApplicationId,
            ResourceType = resourceType,
            AccessType = accessType,
            ResourceKey = ApplicationId.ToString()
        };
}
