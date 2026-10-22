using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class ApplicationMetadataResolverTests
{
    private readonly ApplicationMetadataResolver _resolver = new();

    private static ApplicationMetadataResolveContext DefaultContext() =>
        new(
            "TRF-20261012-001",
            "1.1.117",
            "Owen Lakin",
            "owen@example.com",
            "Lead applicant",
            "Application",
            new Dictionary<string, object> { ["trustName"] = "Example Trust" });

    [Fact]
    public void Resolve_uses_default_rows_when_applicationMetaData_is_null()
    {
        var template = new FormTemplate
        {
            TemplateId = "t1",
            TemplateName = "Test",
            Description = "d",
            TaskGroups = [MinimalTaskGroup()],
            ApplicationMetaData = null
        };

        var rows = _resolver.Resolve(template, DefaultContext());

        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public void Resolve_uses_default_rows_when_applicationMetaData_missing()
    {
        var template = new FormTemplate
        {
            TemplateId = "t1",
            TemplateName = "Test",
            Description = "d",
            TaskGroups = [MinimalTaskGroup()]
        };

        var rows = _resolver.Resolve(template, DefaultContext());

        Assert.Equal(3, rows.Count);
        Assert.Equal("Application reference", rows[0].Label);
        Assert.Equal("TRF-20261012-001", rows[0].DisplayValue);
        Assert.Equal("1.1.117", rows[1].DisplayValue);
        Assert.Equal("Owen Lakin", rows[2].DisplayValue);
    }

    [Fact]
    public void Resolve_uses_configured_items_and_application_bindings()
    {
        var template = new FormTemplate
        {
            TemplateId = "t1",
            TemplateName = "Test",
            Description = "d",
            TaskGroups = [MinimalTaskGroup()],
            ApplicationMetaData = new ApplicationMetadataConfiguration
            {
                Items =
                [
                    new ApplicationMetadataItemDefinition { Label = "Application Reference", FieldId = "application.reference" },
                    new ApplicationMetadataItemDefinition { Label = "Trust", FieldId = "field.trustName" }
                ]
            }
        };

        var rows = _resolver.Resolve(template, DefaultContext());

        Assert.Equal(2, rows.Count);
        Assert.Equal("TRF-20261012-001", rows[0].DisplayValue);
        Assert.Equal("Example Trust", rows[1].DisplayValue);
    }

    [Fact]
    public void Resolve_limits_to_max_items()
    {
        var items = Enumerable.Range(1, 12)
            .Select(i => new ApplicationMetadataItemDefinition
            {
                Label = $"Row {i}",
                FieldId = "application.reference"
            })
            .ToList();

        var template = new FormTemplate
        {
            TemplateId = "t1",
            TemplateName = "Test",
            Description = "d",
            TaskGroups = [MinimalTaskGroup()],
            ApplicationMetaData = new ApplicationMetadataConfiguration { Items = items }
        };

        var rows = _resolver.Resolve(template, DefaultContext());

        Assert.Equal(ApplicationMetadataConfiguration.MaxItems, rows.Count);
    }

    private static TaskGroup MinimalTaskGroup() =>
        new()
        {
            GroupId = "g1",
            GroupName = "Group",
            GroupOrder = 1,
            GroupStatus = nameof(Domain.Models.TaskStatus.NotStarted),
            Tasks =
            [
                new Domain.Models.Task
                {
                    TaskId = "task1",
                    TaskName = "Task",
                    TaskOrder = 1,
                    TaskStatusString = nameof(Domain.Models.TaskStatus.NotStarted),
                    Pages = []
                }
            ]
        };
}
