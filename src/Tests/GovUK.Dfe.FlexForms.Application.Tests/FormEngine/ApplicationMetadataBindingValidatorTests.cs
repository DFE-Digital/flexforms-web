using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class ApplicationMetadataBindingValidatorTests
{
    private static readonly FormTemplate Template = new()
    {
        TemplateId = "t1",
        TemplateName = "Test",
        Description = "d",
        TaskGroups =
        [
            new TaskGroup
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
                        Pages =
                        [
                            Page(
                                Field("trustName", "text"),
                                Field("trustLookup", "complexField", new ComplexField { Id = "trusts" }),
                                Field("transfer.date", "date"))
                        ],
                        Summary = new TaskSummaryConfiguration
                        {
                            Mode = "multiCollectionFlow",
                            Flows =
                            [
                                new MultiCollectionFlowConfiguration
                                {
                                    FlowId = "trusts",
                                    FieldId = "detailsOfIncomingTrust",
                                    Pages =
                                    [
                                        Page(
                                            Field("incomingTrustsSearch-field-flow", "autocomplete"),
                                            Field("incomingTrustContact", "email"))
                                    ]
                                }
                            ],
                            DerivedFlows =
                            [
                                new DerivedCollectionFlowConfiguration
                                {
                                    FlowId = "declarations",
                                    FieldId = "trustDeclarations",
                                    Pages = [Page(Field("signatoryName", "text"))]
                                }
                            ]
                        }
                    }
                ]
            }
        ]
    };

    [Theory]
    [InlineData("application.reference")]
    [InlineData("application.version")]
    [InlineData("application.leadApplicant")]
    [InlineData("application.leadApplicantName")]
    [InlineData("APPLICATION.LEADAPPLICANTEMAIL")]
    [InlineData("application.field.trustName")]
    [InlineData("field.trustName")]
    [InlineData("trustName")]
    [InlineData("TRUSTNAME")]
    [InlineData("transfer.date")]
    [InlineData("trustLookup.ukprn")]
    [InlineData("trustLookup[0].name")]
    [InlineData("detailsOfIncomingTrust")]
    [InlineData("detailsOfIncomingTrust[1]")]
    [InlineData("detailsOfIncomingTrust.incomingTrustsSearch-field-flow")]
    [InlineData("detailsOfIncomingTrust[0].incomingTrustsSearch-field-flow.name")]
    [InlineData("detailsOfIncomingTrust.incomingTrustContact")]
    [InlineData("incomingTrustsSearch-field-flow.name")]
    [InlineData("incomingTrustContact")]
    [InlineData("trustDeclarations[0].signatoryName")]
    [InlineData("  field.trustName  ")]
    public void Validate_accepts_paths_that_exist_in_the_template(string binding)
    {
        Assert.Null(ApplicationMetadataBindingValidator.Validate(binding, Template));
    }

    [Fact]
    public void Validate_rejects_unknown_application_key_and_suggests_closest()
    {
        var problem = ApplicationMetadataBindingValidator.Validate("application.refrence", Template);

        Assert.NotNull(problem);
        Assert.Contains("'refrence' is not a recognised application value", problem);
        Assert.Contains("'application.leadApplicantEmail'", problem);
        Assert.Contains("Did you mean 'application.reference'?", problem);
    }

    [Theory]
    [InlineData("field.trustNam")]
    [InlineData("trustNam")]
    [InlineData("application.field.trustNam")]
    public void Validate_rejects_unknown_field_and_suggests_closest(string binding)
    {
        var problem = ApplicationMetadataBindingValidator.Validate(binding, Template);

        Assert.Equal("no field or collection with ID 'trustNam' exists in the template. Did you mean 'trustName'?", problem);
    }

    [Fact]
    public void Validate_rejects_unknown_field_without_suggestion_when_nothing_is_close()
    {
        var problem = ApplicationMetadataBindingValidator.Validate("field.completelyDifferent", Template);

        Assert.Equal("no field or collection with ID 'completelyDifferent' exists in the template", problem);
    }

    [Fact]
    public void Validate_rejects_field_missing_from_named_collection_and_lists_its_fields()
    {
        var problem = ApplicationMetadataBindingValidator.Validate(
            "detailsOfIncomingTrust[0].incomingTrustSearch-field-flow.name",
            Template);

        Assert.NotNull(problem);
        Assert.Contains("'incomingTrustSearch-field-flow' is not a field in collection 'detailsOfIncomingTrust'", problem);
        Assert.Contains("'incomingTrustsSearch-field-flow', 'incomingTrustContact'", problem);
        Assert.Contains("Did you mean 'incomingTrustsSearch-field-flow'?", problem);
    }

    [Theory]
    [InlineData("field.trustName.name", "trustName", "text", ".name")]
    [InlineData("detailsOfIncomingTrust.incomingTrustContact.address", "incomingTrustContact", "email", ".address")]
    [InlineData("incomingTrustContact.a.b", "incomingTrustContact", "email", ".a.b")]
    public void Validate_rejects_child_property_on_plain_value_field(string binding, string fieldId, string type, string property)
    {
        var problem = ApplicationMetadataBindingValidator.Validate(binding, Template);

        Assert.NotNull(problem);
        Assert.Contains($"'{fieldId}' is a '{type}' field", problem);
        Assert.Contains($"'{property}' cannot be read", problem);
    }

    [Theory]
    [InlineData("trustName[0]", "trustName[0]")]
    [InlineData("detailsOfIncomingTrust.incomingTrustContact[1]", "incomingTrustContact[1]")]
    public void Validate_rejects_index_on_plain_value_field(string binding, string segment)
    {
        var problem = ApplicationMetadataBindingValidator.Validate(binding, Template);

        Assert.NotNull(problem);
        Assert.StartsWith($"'{segment}' cannot use an index", problem);
    }

    [Theory]
    [InlineData("field.")]
    [InlineData("application.field.")]
    public void Validate_rejects_path_without_field(string binding)
    {
        var problem = ApplicationMetadataBindingValidator.Validate(binding, Template);

        Assert.NotNull(problem);
        Assert.StartsWith("the path does not name a field", problem);
    }

    [Theory]
    [InlineData("trustName..name", "trustName..name")]
    [InlineData("detailsOfIncomingTrust[x].name", "detailsOfIncomingTrust[x]")]
    [InlineData("detailsOfIncomingTrust[-1]", "detailsOfIncomingTrust[-1]")]
    [InlineData("detailsOfIncomingTrust[0", "detailsOfIncomingTrust[0")]
    [InlineData("field.trustName.", "trustName.")]
    public void Validate_rejects_malformed_path(string binding, string reported)
    {
        var problem = ApplicationMetadataBindingValidator.Validate(binding, Template);

        Assert.NotNull(problem);
        Assert.StartsWith($"'{reported}' is not a valid path", problem);
    }

    [Fact]
    public void Validate_accepts_every_application_key_the_resolver_maps()
    {
        var template = new FormTemplate
        {
            TemplateId = "t1",
            TemplateName = "Test",
            Description = "d",
            TaskGroups = Template.TaskGroups,
            ApplicationMetaData = new ApplicationMetadataConfiguration
            {
                Items = ApplicationMetadataBindingValidator.ApplicationKeys
                    .Select(key => new ApplicationMetadataItemDefinition { Label = key, FieldId = $"application.{key}" })
                    .ToList()
            }
        };
        var context = new ApplicationMetadataResolveContext(
            "REF-1", "1.0.0", "Lead", "lead@example.com", "Lead applicant", "Application", null);

        var rows = new ApplicationMetadataResolver().Resolve(template, context);

        Assert.All(rows, row => Assert.False(string.IsNullOrEmpty(row.DisplayValue), $"{row.Label} resolved to empty"));
    }

    private static Page Page(params Field[] fields) =>
        new()
        {
            PageId = "p1",
            Slug = "p1",
            Title = "Page",
            Description = "d",
            PageOrder = 1,
            Fields = [.. fields]
        };

    private static Field Field(string fieldId, string type, ComplexField? complexField = null) =>
        new()
        {
            FieldId = fieldId,
            Type = type,
            Label = new Label { Value = fieldId },
            Order = 1,
            ComplexField = complexField
        };
}
