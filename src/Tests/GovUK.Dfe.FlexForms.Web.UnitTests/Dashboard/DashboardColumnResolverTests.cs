using GovUK.Dfe.FlexForms.Application.Dashboard;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Dashboard;

public class DashboardColumnResolverTests
{
    [Fact]
    public void Resolve_WhenNoDashboard_ReturnsDefaultSystemColumns()
    {
        var template = CreateTemplate(dashboard: null);

        var columns = DashboardColumnResolver.Resolve(template);

        Assert.Equal(5, columns.Count);
        Assert.All(columns, c => Assert.Equal(DashboardColumnKind.System, c.Kind));
        Assert.Equal(
            [
                DashboardColumnResolver.SystemReference,
                DashboardColumnResolver.SystemDateStarted,
                DashboardColumnResolver.SystemDateSubmitted,
                DashboardColumnResolver.SystemStatus,
                DashboardColumnResolver.SystemAction
            ],
            columns.Select(c => c.Key));
    }

    [Fact]
    public void Resolve_WhenOnlyFieldColumns_MergesDefaultsAndRespectsOrder()
    {
        var template = CreateTemplate(new DashboardConfiguration
        {
            Columns =
            [
                new DashboardColumnDefinition
                {
                    FieldId = "incomingTrustName",
                    Header = "Trust name",
                    Order = 15
                }
            ]
        });

        var columns = DashboardColumnResolver.Resolve(template);

        Assert.Equal(6, columns.Count);
        Assert.Equal(DashboardColumnResolver.SystemReference, columns[0].Key);
        Assert.Equal("field:incomingTrustName", columns[1].Key);
        Assert.Equal("Trust name", columns[1].Header);
        Assert.Equal(DashboardColumnResolver.SystemDateStarted, columns[2].Key);
    }

    [Fact]
    public void Resolve_CapsCustomFieldColumnsAtThree()
    {
        var template = CreateTemplate(new DashboardConfiguration
        {
            Columns =
            [
                new DashboardColumnDefinition { FieldId = "a", Header = "A", Order = 1 },
                new DashboardColumnDefinition { FieldId = "b", Header = "B", Order = 2 },
                new DashboardColumnDefinition { FieldId = "c", Header = "C", Order = 3 },
                new DashboardColumnDefinition { FieldId = "d", Header = "D", Order = 4 }
            ]
        });

        var columns = DashboardColumnResolver.Resolve(template);

        Assert.Equal(3, columns.Count(c => c.Kind == DashboardColumnKind.Field));
        Assert.DoesNotContain(columns, c => c.FieldId == "d");
    }

    [Fact]
    public void Resolve_FullLayout_UsesConfiguredSystemAndFieldOrder()
    {
        var template = CreateTemplate(new DashboardConfiguration
        {
            Columns =
            [
                new DashboardColumnDefinition { Type = "system", Id = "reference", Order = 10 },
                new DashboardColumnDefinition
                {
                    Type = "field",
                    FieldId = "proposedTransferDate",
                    Header = "Proposed date",
                    Order = 20
                },
                new DashboardColumnDefinition { Type = "system", Id = "status", Order = 30 },
                new DashboardColumnDefinition { Type = "system", Id = "action", Order = 40 }
            ]
        });

        var columns = DashboardColumnResolver.Resolve(template);

        Assert.Equal(
            ["reference", "field:proposedTransferDate", "status", "action"],
            columns.Select(c => c.Key));
    }

    private static FormTemplate CreateTemplate(DashboardConfiguration? dashboard) =>
        new()
        {
            TemplateId = "t1",
            TemplateName = "Test",
            Description = "Test",
            TaskGroups = [],
            Dashboard = dashboard
        };
}
