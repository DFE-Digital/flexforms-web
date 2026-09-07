using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;
using NSubstitute;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.ViewModels.FormEngine;

public class FormEnginePresentationComposerTests
{
    private readonly IFieldFormattingService _formatting = Substitute.For<IFieldFormattingService>();
    private readonly IComplexFieldConfigurationService _complexFields = Substitute.For<IComplexFieldConfigurationService>();
    private readonly IInfectedUploadFilter _infectedFilter = Substitute.For<IInfectedUploadFilter>();
    private readonly IDerivedCollectionFlowService _derivedFlows = Substitute.For<IDerivedCollectionFlowService>();
    private readonly FormEnginePresentationComposer _composer;

    public FormEnginePresentationComposerTests()
    {
        _infectedFilter.FilterList(Arg.Any<IReadOnlyList<UploadDto>>(), Arg.Any<string?>())
            .Returns(call => (call.Arg<IReadOnlyList<UploadDto>>() ?? []).ToList());
        _complexFields.GetConfiguration(Arg.Any<string>())
            .Returns(call => new ComplexFieldConfiguration { Id = call.Arg<string>() });
        _composer = new FormEnginePresentationComposer(
            _formatting, _complexFields, _infectedFilter, _derivedFlows);
    }

    [Fact]
    public void BuildPreview_maps_regular_text_and_radio_fields()
    {
        var radio = Field("choice", "radios", "Choice", [new Option { Value = "yes", Label = "Yes" }]);
        var text = Field("notes", "text", "Notes");
        var task = Task("t1", "About you", pages: [Page("p1", [radio, text])]);
        var formData = new Dictionary<string, object>
        {
            ["choice"] = "yes",
            ["notes"] = "Line1\nLine2"
        };

        _formatting.GetFieldValue("choice", formData).Returns("yes");
        _formatting.HasFieldValue("choice", formData).Returns(true);
        _formatting.GetFieldValue("notes", formData).Returns("Line1\nLine2");
        _formatting.HasFieldValue("notes", formData).Returns(true);

        var preview = _composer.BuildPreview(Context(formData, Template(task)));

        var rows = preview.Groups.Single().Tasks.Single().Rows;
        Assert.Equal("Choice", rows[0].Key);
        Assert.Equal(SummaryDisplayKind.Text, rows[0].Value.Kind);
        Assert.Equal("Yes", rows[0].Value.Text);
        Assert.Equal(SummaryDisplayKind.Html, rows[1].Value.Kind);
        Assert.Equal("Line1<br/>Line2", rows[1].Value.Html);
        Assert.True(preview.Submit.ShowSubmitSection);
    }

    [Fact]
    public void BuildPreview_renders_upload_json_as_download_links()
    {
        var fileId = Guid.NewGuid();
        var uploadJson = JsonSerializer.Serialize(new[]
        {
            new UploadDto { Id = fileId, OriginalFileName = "evidence.pdf" }
        });
        var field = Field("files", "upload", "Evidence");
        var task = Task("t1", "Evidence", pages: [Page("p1", [field])]);
        var formData = new Dictionary<string, object> { ["files"] = uploadJson };

        _formatting.GetFieldValue("files", formData).Returns(uploadJson);
        _formatting.HasFieldValue("files", formData).Returns(true);
        _formatting.GetFormattedFieldValues("files", formData).Returns(["evidence.pdf"]);
        _formatting.GetFieldItemLabel("files", Arg.Any<FormTemplate>()).Returns("File");
        _formatting.IsFieldAllowMultiple("files", Arg.Any<FormTemplate>()).Returns(false);

        var preview = _composer.BuildPreview(Context(formData, Template(task)));
        var value = preview.Groups.Single().Tasks.Single().Rows.Single().Value;

        Assert.Equal(SummaryDisplayKind.UploadFiles, value.Kind);
        Assert.Equal(fileId, value.Files.Single().FileId);
        Assert.Equal("evidence.pdf", value.Files.Single().FileName);
        Assert.False(value.WrapFilesInDivs);
    }

    [Fact]
    public void BuildCollectionFlows_deserializes_items_and_respects_max_items()
    {
        var nameField = Field("fullName", "text", "Name");
        var flow = new MultiCollectionFlowConfiguration
        {
            FlowId = "members",
            FieldId = "memberList",
            Title = "Members",
            ItemKind = "Member",
            ItemKindPlural = "Members",
            AddButtonLabel = "Add member",
            MaxItems = 2,
            TableType = "card",
            ItemTitleBinding = "fullName",
            SummaryColumns = [new FlowSummaryColumn { Field = "fullName", Label = "Full name" }],
            Pages = [Page("p1", [nameField])]
        };
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var items = JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, object> { ["id"] = "i1", ["fullName"] = "Ada" },
            new Dictionary<string, object> { ["id"] = "i2", ["fullName"] = "Grace" }
        });
        var formData = new Dictionary<string, object> { ["memberList"] = items };

        _formatting.GetFormattedFieldValues("fullName", Arg.Any<Dictionary<string, object>>())
            .Returns(call =>
            {
                var data = call.Arg<Dictionary<string, object>>();
                return [data["fullName"].ToString() ?? string.Empty];
            });

        var sections = _composer.BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task);
        var section = sections.Single();

        Assert.Equal("Members", section.Title);
        Assert.False(section.CanAddMore);
        Assert.False(section.IsListStyle);
        Assert.Equal(2, section.Items.Count);
        Assert.Equal("Ada", section.Items[0].Title);
        Assert.Equal("i1", section.Items[0].ItemId);
        Assert.Equal("Full name", section.Items[0].Rows.Single().Key);
        Assert.Equal("Ada", section.Items[0].Rows.Single().Value.Html);
        Assert.Contains("/flow/members/", section.Items[0].Rows.Single().ChangeUrl);
        Assert.Equal("remove-flow-item-@memberNumber", section.Items[0].Remove.ButtonId);
        _infectedFilter.DidNotReceiveWithAnyArgs().FilterList(default!, default);
    }

    [Fact]
    public void BuildCollectionFlows_filters_infected_uploads()
    {
        var uploadField = Field("files", "complexField", "Files", complexFieldId: "upload-1");
        _complexFields.GetConfiguration("upload-1")
            .Returns(new ComplexFieldConfiguration { Id = "upload-1", FieldType = "upload" });

        var safeId = Guid.NewGuid();
        var infectedId = Guid.NewGuid();
        var uploadJson = JsonSerializer.Serialize(new[]
        {
            new UploadDto { Id = safeId, OriginalFileName = "ok.pdf" },
            new UploadDto { Id = infectedId, OriginalFileName = "bad.pdf" }
        });

        var flow = new MultiCollectionFlowConfiguration
        {
            FlowId = "docs",
            FieldId = "docList",
            Title = "Documents",
            TableType = "list",
            SummaryColumns = [new FlowSummaryColumn { Field = "files", Label = "Files" }],
            Pages = [Page("p1", [uploadField])]
        };
        var task = Task("t1", "Docs", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var items = JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, object> { ["id"] = "i1", ["files"] = uploadJson }
        });
        var formData = new Dictionary<string, object> { ["docList"] = items };

        _formatting.GetFormattedFieldValues("files", Arg.Any<Dictionary<string, object>>())
            .Returns(["ok.pdf", "bad.pdf"]);
        _infectedFilter.FilterList(Arg.Any<IReadOnlyList<UploadDto>>(), Arg.Any<string?>())
            .Returns(call => call.Arg<IReadOnlyList<UploadDto>>()!.Where(f => f.Id == safeId).ToList());

        var section = _composer.BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task).Single();
        var value = section.Items.Single().Rows.Single().Value;

        Assert.True(section.IsListStyle);
        Assert.Equal(SummaryDisplayKind.UploadFiles, value.Kind);
        Assert.Equal(safeId, value.Files.Single().FileId);
        Assert.True(value.WrapFilesInDivs);
        _infectedFilter.Received().FilterList(Arg.Any<IReadOnlyList<UploadDto>>(), "app-1");
    }

    [Fact]
    public void BuildPreview_builds_collection_flow_preview_rows()
    {
        var roleField = Field("role", "text", "Role");
        var hiddenField = Field("secret", "text", "Secret");
        var flow = new MultiCollectionFlowConfiguration
        {
            FlowId = "members",
            FieldId = "memberList",
            Title = "Team members",
            ItemKind = "Member",
            ItemTitleBinding = "fullName",
            SummaryColumns =
            [
                new FlowSummaryColumn { Field = "fullName", Label = "Full name" },
                new FlowSummaryColumn { Field = "role", Label = "Role" },
                new FlowSummaryColumn { Field = "secret", Label = "Secret" }
            ],
            Pages = [Page("p1", [roleField, hiddenField])]
        };
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var items = JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, object> { ["id"] = "i1", ["fullName"] = "Ada", ["role"] = "Lead" }
        });
        var formData = new Dictionary<string, object> { ["memberList"] = items };

        _formatting.GetFormattedFieldValues("fullName", Arg.Any<Dictionary<string, object>>())
            .Returns(call => [call.Arg<Dictionary<string, object>>()["fullName"].ToString() ?? ""]);
        _formatting.GetFormattedFieldValues("role", Arg.Any<Dictionary<string, object>>())
            .Returns(call => [call.Arg<Dictionary<string, object>>()["role"].ToString() ?? ""]);

        var preview = _composer.BuildPreview(Context(
            formData,
            Template(task),
            isFieldHiddenForItem: (fieldId, _) => fieldId == "secret"));

        var rows = preview.Groups.Single().Tasks.Single().Rows;
        Assert.Equal("Team members", rows[0].Key);
        Assert.Equal("Ada", rows[1].Key);
        Assert.Equal("Full name", rows[2].Key);
        Assert.Equal("Ada", rows[2].Value.Html);
        Assert.Equal("Role", rows[3].Key);
        Assert.Equal("Lead", rows[3].Value.Html);
        Assert.DoesNotContain(rows, r => r.Key == "Secret");
    }

    [Fact]
    public void BuildPreview_collection_flow_preview_shows_not_answered_when_empty()
    {
        var flow = new MultiCollectionFlowConfiguration
        {
            FlowId = "members",
            FieldId = "memberList",
            Title = "Members",
            SummaryColumns = [new FlowSummaryColumn { Field = "fullName", Label = "Full name" }]
        };
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);

        var preview = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)));
        var rows = preview.Groups.Single().Tasks.Single().Rows;

        Assert.Equal("Members", rows[0].Key);
        Assert.Equal("No items added", rows[1].Key);
        Assert.Equal(SummaryDisplayKind.NotAnswered, rows[1].Value.Kind);
    }

    [Fact]
    public void BuildPreview_collection_flow_preview_uses_item_index_when_title_binding_missing()
    {
        var flow = new MultiCollectionFlowConfiguration
        {
            FlowId = "items",
            FieldId = "itemList",
            Title = "Items",
            SummaryColumns = [new FlowSummaryColumn { Field = "note", Label = "Note" }]
        };
        var task = Task("t1", "Items", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var items = JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, object> { ["id"] = "i1", ["note"] = "First" }
        });
        var formData = new Dictionary<string, object> { ["itemList"] = items };

        _formatting.GetFormattedFieldValues("note", Arg.Any<Dictionary<string, object>>())
            .Returns(call => [call.Arg<Dictionary<string, object>>()["note"].ToString() ?? ""]);

        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal("Item 1", rows[1].Key);
    }

    [Fact]
    public void BuildPreview_skips_hidden_regular_fields()
    {
        var visible = Field("visible", "text", "Visible");
        var hidden = Field("hidden", "text", "Hidden");
        var task = Task("t1", "About you", pages: [Page("p1", [visible, hidden])]);
        var formData = new Dictionary<string, object>
        {
            ["visible"] = "shown",
            ["hidden"] = "secret"
        };

        _formatting.GetFieldValue("visible", formData).Returns("shown");
        _formatting.HasFieldValue("visible", formData).Returns(true);

        var rows = _composer.BuildPreview(Context(
                formData,
                Template(task),
                isFieldHidden: fieldId => fieldId == "hidden"))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Single(rows);
        Assert.Equal("Visible", rows[0].Key);
    }

    [Fact]
    public void BuildPreview_regular_fields_cover_select_and_empty_values()
    {
        var select = Field("region", "select", "Region", [new Option { Value = "ne", Label = "North East" }]);
        var empty = Field("notes", "text", "Notes");
        var task = Task("t1", "About you", pages: [Page("p1", [select, empty])]);
        var formData = new Dictionary<string, object> { ["region"] = "ne" };

        _formatting.GetFieldValue("region", formData).Returns("ne");
        _formatting.HasFieldValue("region", formData).Returns(true);
        _formatting.GetFieldValue("notes", formData).Returns(string.Empty);
        _formatting.HasFieldValue("notes", formData).Returns(false);

        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal("North East", rows[0].Value.Text);
        Assert.Equal(SummaryDisplayKind.NotAnswered, rows[1].Value.Kind);
    }

    [Fact]
    public void BuildPreview_renders_autocomplete_and_multiple_complex_values()
    {
        var trustJson = """{"name":"Contoso Trust","postcode":"SW1A 1AA"}""";
        var autocomplete = Field("trust", "autocomplete", "Trust");
        var multi = Field("tags", "complexField", "Tags", complexFieldId: "tag-field");
        var task = Task("t1", "Organisation", pages: [Page("p1", [autocomplete, multi])]);
        var formData = new Dictionary<string, object>
        {
            ["trust"] = trustJson,
            ["tags"] = "placeholder"
        };

        _formatting.GetFieldValue("trust", formData).Returns(trustJson);
        _formatting.HasFieldValue("trust", formData).Returns(true);
        _formatting.GetFormattedFieldValues("trust", formData).Returns(["Contoso Trust"]);
        _formatting.GetFieldItemLabel("trust", Arg.Any<FormTemplate>()).Returns("Trust");
        _formatting.IsFieldAllowMultiple("trust", Arg.Any<FormTemplate>()).Returns(false);

        _formatting.GetFieldValue("tags", formData).Returns("x");
        _formatting.HasFieldValue("tags", formData).Returns(true);
        _formatting.GetFormattedFieldValues("tags", formData).Returns(["Alpha", "Beta"]);
        _formatting.GetFieldItemLabel("tags", Arg.Any<FormTemplate>()).Returns("Tag");
        _formatting.IsFieldAllowMultiple("tags", Arg.Any<FormTemplate>()).Returns(true);

        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal(SummaryDisplayKind.AutocompleteHtml, rows[0].Value.Kind);
        Assert.Contains("Contoso Trust", rows[0].Value.Html);
        Assert.Equal("Tags", rows[1].Key);
        Assert.Equal(SummaryDisplayKind.Empty, rows[1].Value.Kind);
        Assert.Equal("Tag 1", rows[2].Key);
        Assert.Equal("Alpha", rows[2].Value.Html);
        Assert.Equal("Tag 2", rows[3].Key);
        Assert.Equal("Beta", rows[3].Value.Html);
    }

    [Fact]
    public void BuildCollectionFlows_returns_empty_when_task_has_no_flows()
    {
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: []);
        var sections = _composer.BuildCollectionFlows(Context(new Dictionary<string, object>(), Template(task), taskId: "t1"), task);
        Assert.Empty(sections);
    }

    [Fact]
    public void BuildCollectionFlows_card_style_renders_autocomplete_and_checkboxes()
    {
        var autocompleteField = Field("org", "complexField", "Organisation", complexFieldId: "org-auto");
        var checkboxField = Field("roles", "checkboxes", "Roles", [
            new Option { Value = "lead", Label = "Lead" },
            new Option { Value = "member", Label = "Member" }
        ]);
        _complexFields.GetConfiguration("org-auto")
            .Returns(new ComplexFieldConfiguration { Id = "org-auto", FieldType = "autocomplete" });

        var orgJson = """{"name":"Contoso Trust","postcode":"SW1A 1AA"}""";
        var flow = new MultiCollectionFlowConfiguration
        {
            FlowId = "members",
            FieldId = "memberList",
            Title = "Members",
            TableType = "card",
            SummaryColumns =
            [
                new FlowSummaryColumn { Field = "org", Label = "Organisation" },
                new FlowSummaryColumn { Field = "roles", Label = "Roles" }
            ],
            Pages = [Page("p1", [autocompleteField, checkboxField])]
        };
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var items = JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, object>
            {
                ["id"] = "i1",
                ["org"] = orgJson,
                ["roles"] = new[] { "lead", "member" }
            }
        });
        var formData = new Dictionary<string, object> { ["memberList"] = items };

        _formatting.GetFormattedFieldValues("org", Arg.Any<Dictionary<string, object>>())
            .Returns(call => [call.Arg<Dictionary<string, object>>()["org"].ToString() ?? ""]);
        _formatting.GetFormattedFieldValues("roles", Arg.Any<Dictionary<string, object>>())
            .Returns(["Lead", "Member"]);

        var section = _composer.BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task).Single();
        var rows = section.Items.Single().Rows;

        Assert.False(section.IsListStyle);
        Assert.True(section.CanAddMore);
        Assert.Equal(SummaryDisplayKind.AutocompleteHtml, rows[0].Value.Kind);
        Assert.Contains("Contoso Trust", rows[0].Value.Html);
        Assert.Equal(SummaryDisplayKind.HtmlList, rows[1].Value.Kind);
        Assert.Contains("Lead", rows[1].Value.HtmlItems[0]);
    }

    [Fact]
    public void BuildCollectionFlows_list_style_renders_checkbox_values()
    {
        var checkboxField = Field("roles", "checkboxes", "Roles", [
            new Option { Value = "lead", Label = "Lead" }
        ]);
        var flow = new MultiCollectionFlowConfiguration
        {
            FlowId = "members",
            FieldId = "memberList",
            Title = "Members",
            TableType = "list",
            SummaryColumns = [new FlowSummaryColumn { Field = "roles", Label = "Roles" }],
            Pages = [Page("p1", [checkboxField])]
        };
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var items = JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, object> { ["id"] = "i1", ["roles"] = new[] { "lead" } }
        });
        var formData = new Dictionary<string, object> { ["memberList"] = items };

        _formatting.GetFormattedFieldValues("roles", Arg.Any<Dictionary<string, object>>())
            .Returns(["Lead"]);

        var value = _composer.BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single().Items.Single().Rows.Single().Value;

        Assert.Equal(SummaryDisplayKind.Checkboxes, value.Kind);
        Assert.Equal("lead", value.Checkboxes.Single());
    }

    [Fact]
    public void BuildPreview_builds_derived_status_rows()
    {
        var derived = new DerivedCollectionFlowConfiguration
        {
            FlowId = "sign",
            Title = "Declarations",
            SourceFieldId = "orgs",
            FieldId = "decls",
            EmptyStateMessage = "Nothing here",
            Pages = [Page("p1", [Field("chair", "text", "Chair")])]
        };
        var task = Task(
            "t1",
            "Sign",
            mode: FormStepPolicy.DerivedCollectionFlowMode,
            derivedFlows: [derived]);

        _derivedFlows.GenerateItemsFromSourceField("orgs", Arg.Any<Dictionary<string, object>>(), derived)
            .Returns([new DerivedCollectionItem { Id = "d1", DisplayName = "Trust A" }]);
        _derivedFlows.GetItemStatuses("decls", Arg.Any<Dictionary<string, object>>())
            .Returns(new Dictionary<string, string> { ["d1"] = "Signed" });
        _derivedFlows.GetItemDeclarationData("decls", "d1", Arg.Any<Dictionary<string, object>>())
            .Returns(new Dictionary<string, object> { ["chair"] = "Jane" });

        var preview = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)));
        var rows = preview.Groups.Single().Tasks.Single().Rows;

        Assert.Equal("Declarations", rows[0].Key);
        Assert.True(rows[0].KeyIsBold);
        Assert.Equal("Trust A", rows[1].Key);
        Assert.True(rows[1].Value.StatusIsSigned);
        Assert.Equal("Chair", rows[2].Key);
        Assert.Equal("Jane", rows[2].Value.Html);
    }

    [Fact]
    public void BuildPreview_derived_flow_shows_empty_state()
    {
        var derived = new DerivedCollectionFlowConfiguration
        {
            FlowId = "sign",
            Title = "Declarations",
            SourceFieldId = "orgs",
            FieldId = "decls",
            EmptyStateMessage = "Nothing to sign",
            Pages = [Page("p1", [Field("chair", "text", "Chair")])]
        };
        var task = Task("t1", "Sign", mode: FormStepPolicy.DerivedCollectionFlowMode, derivedFlows: [derived]);

        _derivedFlows.GenerateItemsFromSourceField("orgs", Arg.Any<Dictionary<string, object>>(), derived)
            .Returns([]);

        var rows = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal("No items", rows[1].Key);
        Assert.Contains("Nothing to sign", rows[1].Value.Html);
    }

    [Fact]
    public void BuildPreview_derived_flow_renders_radios_select_checkboxes_and_empty_values()
    {
        var derived = new DerivedCollectionFlowConfiguration
        {
            FlowId = "sign",
            Title = "Declarations",
            SourceFieldId = "orgs",
            FieldId = "decls",
            Pages =
            [
                Page("p1",
                [
                    Field("choice", "radios", "Choice", [new Option { Value = "yes", Label = "Yes" }]),
                    Field("region", "select", "Region", [new Option { Value = "ne", Label = "North East" }]),
                    Field("tags", "checkboxes", "Tags", [new Option { Value = "a", Label = "Tag A" }]),
                    Field("notes", "text", "Notes")
                ])
            ]
        };
        var task = Task("t1", "Sign", mode: FormStepPolicy.DerivedCollectionFlowMode, derivedFlows: [derived]);

        _derivedFlows.GenerateItemsFromSourceField("orgs", Arg.Any<Dictionary<string, object>>(), derived)
            .Returns([new DerivedCollectionItem { Id = "d1", DisplayName = "Trust A" }]);
        _derivedFlows.GetItemStatuses("decls", Arg.Any<Dictionary<string, object>>())
            .Returns(new Dictionary<string, string> { ["d1"] = "Not signed yet" });
        _derivedFlows.GetItemDeclarationData("decls", "d1", Arg.Any<Dictionary<string, object>>())
            .Returns(new Dictionary<string, object>
            {
                ["choice"] = "yes",
                ["region"] = "ne",
                ["tags"] = "a",
                ["notes"] = string.Empty
            });
        _formatting.GetFormattedFieldValues("tags", Arg.Any<Dictionary<string, object>>())
            .Returns(["a"]);

        var formData = new Dictionary<string, object> { ["tags"] = "a" };
        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.False(rows[1].Value.StatusIsSigned);
        Assert.Equal("Yes", rows[2].Value.Html);
        Assert.Equal("North East", rows[3].Value.Html);
        Assert.Equal(SummaryDisplayKind.Checkboxes, rows[4].Value.Kind);
        Assert.Equal("Tag A", rows[4].Value.Checkboxes.Single());
        Assert.Equal(SummaryDisplayKind.NotAnswered, rows[5].Value.Kind);
    }

    [Fact]
    public void BuildPreview_collection_flow_renders_items_and_empty_state()
    {
        var flow = new MultiCollectionFlowConfiguration
        {
            FlowId = "members",
            FieldId = "memberList",
            Title = "Members",
            ItemKind = "Member",
            ItemTitleBinding = "fullName",
            SummaryColumns = [new FlowSummaryColumn { Field = "fullName", Label = "Full name" }],
            Pages = [Page("p1", [Field("fullName", "text", "Name")])]
        };
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var items = JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, object> { ["id"] = "i1", ["fullName"] = "Ada" }
        });
        var formData = new Dictionary<string, object> { ["memberList"] = items };

        _formatting.GetFormattedFieldValues("fullName", Arg.Any<Dictionary<string, object>>())
            .Returns(["Ada"]);

        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal("Members", rows[0].Key);
        Assert.True(rows[0].KeyIsBold);
        Assert.Equal("Ada", rows[1].Key);
        Assert.Equal("Full name", rows[2].Key);
        Assert.Equal("Ada", rows[2].Value.Html);

        var emptyRows = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows;
        Assert.Equal("No items added", emptyRows[1].Key);
        Assert.Equal(SummaryDisplayKind.NotAnswered, emptyRows[1].Value.Kind);
    }

    private static FormEnginePresentationContext Context(
        Dictionary<string, object> formData,
        FormTemplate template,
        string taskId = "",
        Func<string, bool>? isFieldHidden = null,
        Func<string, Dictionary<string, object>, bool>? isFieldHiddenForItem = null) =>
        new()
        {
            Template = template,
            FormData = formData,
            ReferenceNumber = "REF-1",
            TaskId = taskId,
            ApplicationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            InfectedFilterApplicationId = "app-1",
            IsEditable = true,
            IsLeadApplicant = true,
            EnsureItemFieldVisibility = (_, _) => { },
            IsFieldHiddenForItem = isFieldHiddenForItem ?? ((_, _) => false),
            IsFieldHidden = isFieldHidden ?? (_ => false)
        };

    private static FormTemplate Template(TaskModel task) =>
        new()
        {
            TemplateId = "tpl",
            TemplateName = "tpl",
            Description = "tpl",
            TaskGroups =
            [
                new TaskGroup
                {
                    GroupId = "g1",
                    GroupName = "Group One",
                    GroupOrder = 1,
                    GroupStatus = "NotStarted",
                    Tasks = [task]
                }
            ]
        };

    private static TaskModel Task(
        string id,
        string name,
        string mode = "standard",
        List<Page>? pages = null,
        List<MultiCollectionFlowConfiguration>? flows = null,
        List<DerivedCollectionFlowConfiguration>? derivedFlows = null) =>
        new()
        {
            TaskId = id,
            TaskName = name,
            TaskOrder = 1,
            TaskStatusString = "NotStarted",
            Pages = pages,
            Summary = new TaskSummaryConfiguration
            {
                Mode = mode,
                Flows = flows,
                DerivedFlows = derivedFlows
            }
        };

    private static Page Page(string id, List<Field> fields) =>
        new()
        {
            PageId = id,
            Slug = id,
            Title = id,
            Description = id,
            PageOrder = 1,
            Fields = fields
        };

    private static Field Field(
        string id,
        string type,
        string label,
        List<Option>? options = null,
        string? complexFieldId = null) =>
        new()
        {
            FieldId = id,
            Type = type,
            Label = new Label { Value = label },
            Order = 1,
            Options = options,
            ComplexField = complexFieldId == null ? null : new ComplexField { Id = complexFieldId }
        };
}
