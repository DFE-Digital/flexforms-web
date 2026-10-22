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
    public void Resolve_reads_indexed_collection_flow_field()
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
                    new ApplicationMetadataItemDefinition
                    {
                        Label = "Second trust",
                        FieldId = "detailsOfIncomingTrust[1].incomingTrustsSearch-field-flow.name",
                        Order = 10
                    }
                ]
            }
        };

        var context = DefaultContext() with
        {
            FormData = new Dictionary<string, object>
            {
                ["detailsOfIncomingTrust"] =
                    """[{"incomingTrustsSearch-field-flow":{"name":"First Trust"}},{"incomingTrustsSearch-field-flow":{"name":"Second Trust"}}]"""
            }
        };

        var rows = _resolver.Resolve(template, context);

        Assert.Single(rows);
        Assert.Equal("Second Trust", rows[0].DisplayValue);
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
    public void Resolve_orders_items_by_order_then_array_index()
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
                    new ApplicationMetadataItemDefinition { Label = "Third", FieldId = "application.reference", Order = 30 },
                    new ApplicationMetadataItemDefinition { Label = "First", FieldId = "application.version", Order = 10 },
                    new ApplicationMetadataItemDefinition { Label = "Second", FieldId = "application.leadApplicant", Order = 20 }
                ]
            }
        };

        var rows = _resolver.Resolve(template, DefaultContext());

        Assert.Equal(["First", "Second", "Third"], rows.Select(r => r.Label).ToArray());
    }

    [Fact]
    public void Resolve_uses_default_rows_when_items_list_is_empty()
    {
        var template = new FormTemplate
        {
            TemplateId = "t1",
            TemplateName = "Test",
            Description = "d",
            TaskGroups = [MinimalTaskGroup()],
            ApplicationMetaData = new ApplicationMetadataConfiguration { Items = [] }
        };

        var rows = _resolver.Resolve(template, DefaultContext());

        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public void Resolve_skips_items_with_blank_label_or_fieldId()
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
                    new ApplicationMetadataItemDefinition { Label = " ", FieldId = "application.reference" },
                    new ApplicationMetadataItemDefinition { Label = "Valid", FieldId = " " },
                    new ApplicationMetadataItemDefinition { Label = "Ref", FieldId = "application.reference" }
                ]
            }
        };

        var rows = _resolver.Resolve(template, DefaultContext());

        Assert.Single(rows);
        Assert.Equal("Ref", rows[0].Label);
    }

    [Fact]
    public void Resolve_maps_all_application_binding_keys()
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
                    new ApplicationMetadataItemDefinition { Label = "Email", FieldId = "application.leadApplicantEmail" },
                    new ApplicationMetadataItemDefinition { Label = "Name alt", FieldId = "application.leadApplicantName" },
                    new ApplicationMetadataItemDefinition { Label = "Unknown", FieldId = "application.unknownKey" },
                    new ApplicationMetadataItemDefinition { Label = "Top field", FieldId = "trustName" }
                ]
            }
        };

        var context = DefaultContext() with { FormData = new Dictionary<string, object> { ["trustName"] = "Via shorthand" } };
        var rows = _resolver.Resolve(template, context);

        Assert.Equal(4, rows.Count);
        Assert.Equal("owen@example.com", rows[0].DisplayValue);
        Assert.Equal("Owen Lakin", rows[1].DisplayValue);
        Assert.Equal(string.Empty, rows[2].DisplayValue);
        Assert.Equal("Via shorthand", rows[3].DisplayValue);
    }

    [Fact]
    public void Resolve_resolves_application_field_prefix_and_wrapped_form_values()
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
                    new ApplicationMetadataItemDefinition { Label = "Trust", FieldId = "application.field.trustName" }
                ]
            }
        };

        var context = DefaultContext() with
        {
            FormData = new Dictionary<string, object>
            {
                ["trustName"] = new Dictionary<string, object> { ["value"] = "Wrapped Trust" }
            }
        };

        var rows = _resolver.Resolve(template, context);

        Assert.Single(rows);
        Assert.Equal("Wrapped Trust", rows[0].DisplayValue);
    }

    [Fact]
    public void Resolve_handles_null_form_data()
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
                    new ApplicationMetadataItemDefinition { Label = "Ref", FieldId = "application.reference" }
                ]
            }
        };

        var context = DefaultContext() with { FormData = null };
        var rows = _resolver.Resolve(template, context);

        Assert.Single(rows);
        Assert.Equal("TRF-20261012-001", rows[0].DisplayValue);
    }

    [Fact]
    public void Resolve_uses_default_rows_when_items_is_null()
    {
        var template = new FormTemplate
        {
            TemplateId = "t1",
            TemplateName = "Test",
            Description = "d",
            TaskGroups = [MinimalTaskGroup()],
            ApplicationMetaData = new ApplicationMetadataConfiguration { Items = null }
        };

        var rows = _resolver.Resolve(template, DefaultContext());

        Assert.Equal(3, rows.Count);
    }

    [Fact]
    public void Resolve_uses_leadApplicant_binding_alias()
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
                    new ApplicationMetadataItemDefinition { Label = "Lead", FieldId = "application.leadApplicant" }
                ]
            }
        };

        var rows = _resolver.Resolve(template, DefaultContext());

        Assert.Equal("Owen Lakin", Assert.Single(rows).DisplayValue);
    }

    [Fact]
    public void Resolve_default_rows_use_empty_strings_when_version_and_lead_name_missing()
    {
        var template = new FormTemplate
        {
            TemplateId = "t1",
            TemplateName = "Test",
            Description = "d",
            TaskGroups = [MinimalTaskGroup()],
            ApplicationMetaData = null
        };

        var context = DefaultContext() with { FormVersion = null, LeadApplicantName = null };
        var rows = _resolver.Resolve(template, context);

        Assert.Equal(string.Empty, rows[1].DisplayValue);
        Assert.Equal(string.Empty, rows[2].DisplayValue);
    }

    [Fact]
    public void Resolve_handles_empty_form_data_dictionary()
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
                    new ApplicationMetadataItemDefinition { Label = "Trust", FieldId = "field.trustName" }
                ]
            }
        };

        var context = DefaultContext() with { FormData = new Dictionary<string, object>() };
        var rows = _resolver.Resolve(template, context);

        Assert.Equal(string.Empty, Assert.Single(rows).DisplayValue);
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
