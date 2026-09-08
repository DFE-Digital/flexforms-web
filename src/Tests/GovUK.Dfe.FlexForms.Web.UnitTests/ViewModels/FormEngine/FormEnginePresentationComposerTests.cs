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

    [Fact]
    public void BuildPreview_maps_submit_section_from_context()
    {
        var task = Task("t1", "About you");
        var blockingFiles = new List<FileValidationBlockDto>();
        var context = new FormEnginePresentationContext
        {
            Template = Template(task),
            FormData = new Dictionary<string, object>(),
            ReferenceNumber = "REF-1",
            IsEditable = false,
            IsLeadApplicant = false,
            SubmitDisabledByConfig = true,
            SubmitDisabledBannerText = "Submissions are paused.",
            SubmitDisabledHelpText = "Try again later.",
            FileValidationBlocksSubmit = true,
            BlockingFiles = blockingFiles,
            IncludePreviewQuery = true,
            EnsureItemFieldVisibility = (_, _) => { },
            IsFieldHiddenForItem = (_, _) => false,
            IsFieldHidden = _ => false
        };

        var preview = _composer.BuildPreview(context);

        Assert.Equal("REF-1", preview.ReferenceNumber);
        Assert.False(preview.Submit.IsEditable);
        Assert.False(preview.Submit.IsLeadApplicant);
        Assert.False(preview.Submit.ShowSubmitSection);
        Assert.True(preview.Submit.SubmitDisabledByConfig);
        Assert.Equal("Submissions are paused.", preview.Submit.DisabledBannerText);
        Assert.Equal("Try again later.", preview.Submit.DisabledHelpText);
        Assert.True(preview.Submit.FileValidationBlocksSubmit);
        Assert.Same(blockingFiles, preview.Submit.BlockingFiles);
        Assert.True(preview.Submit.IncludePreviewQuery);
    }

    [Fact]
    public void BuildPreview_orders_groups_tasks_pages_and_fields()
    {
        var lateTask = Task(
            "t2",
            "Second task",
            order: 2,
            pages:
            [
                Page("p2", [Field("late", "text", "Late field", order: 2)], order: 2),
                Page("p1", [Field("first", "text", "First field", order: 1)], order: 1)
            ]);
        var earlyTask = Task("t1", "First task", order: 1, pages: [Page("p0", [])]);

        var template = TemplateWithGroups(
            Group("g2", "Group Two", 2, Task("t3", "Third task", pages: [Page("p3", [])])),
            Group("g1", "Group One", 1, lateTask, earlyTask));

        _formatting.HasFieldValue(Arg.Any<string>(), Arg.Any<Dictionary<string, object>>()).Returns(true);
        _formatting.GetFieldValue(Arg.Any<string>(), Arg.Any<Dictionary<string, object>>()).Returns("v");

        var preview = _composer.BuildPreview(Context(new Dictionary<string, object>(), template));

        Assert.Equal(new[] { "Group One", "Group Two" }, preview.Groups.Select(g => g.GroupName).ToArray());
        Assert.Equal(new[] { "group-one", "group-two" }, preview.Groups.Select(g => g.TestId).ToArray());
        Assert.Equal(
            new[] { "First task", "Second task" },
            preview.Groups[0].Tasks.Select(t => t.TaskName).ToArray());
        Assert.Equal("second-task", preview.Groups[0].Tasks[1].TestId);
        Assert.Equal("/applications/REF-1/t2", preview.Groups[0].Tasks[1].ChangeUrl);
        Assert.Equal(
            new[] { "First field", "Late field" },
            preview.Groups[0].Tasks[1].Rows.Select(r => r.Key).ToArray());
    }

    [Fact]
    public void BuildPreview_regular_task_without_pages_produces_no_rows()
    {
        var task = Task("t1", "About you");

        var rows = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Empty(rows);
    }

    [Fact]
    public void BuildPreview_regular_simple_value_falls_back_when_options_are_missing()
    {
        var radios = Field("choice", "radios", "Choice");
        var select = Field("region", "select", "Region");
        var task = Task("t1", "About you", pages: [Page("p1", [radios, select])]);
        var formData = new Dictionary<string, object> { ["choice"] = "yes", ["region"] = "ne" };

        _formatting.GetFieldValue("choice", formData).Returns("yes");
        _formatting.HasFieldValue("choice", formData).Returns(true);
        _formatting.GetFieldValue("region", formData).Returns("ne");
        _formatting.HasFieldValue("region", formData).Returns(true);

        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal(SummaryDisplayKind.Html, rows[0].Value.Kind);
        Assert.Equal("yes", rows[0].Value.Html);
        Assert.Equal(SummaryDisplayKind.Html, rows[1].Value.Kind);
        Assert.Equal("ne", rows[1].Value.Html);
    }

    [Fact]
    public void BuildPreview_regular_simple_value_uses_raw_value_when_option_is_not_matched()
    {
        var radios = Field("choice", "radios", "Choice", [new Option { Value = "yes", Label = "Yes" }]);
        var select = Field("region", "select", "Region", [new Option { Value = "ne", Label = "North East" }]);
        var task = Task("t1", "About you", pages: [Page("p1", [radios, select])]);
        var formData = new Dictionary<string, object> { ["choice"] = "maybe", ["region"] = "sw" };

        _formatting.GetFieldValue("choice", formData).Returns("maybe");
        _formatting.HasFieldValue("choice", formData).Returns(true);
        _formatting.GetFieldValue("region", formData).Returns("sw");
        _formatting.HasFieldValue("region", formData).Returns(true);

        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal("maybe", rows[0].Value.Text);
        Assert.Equal("sw", rows[1].Value.Text);
    }

    [Fact]
    public void BuildPreview_regular_complex_field_shows_not_answered_without_formatted_values()
    {
        var field = Field("trust", "autocomplete", "Trust");
        var task = Task("t1", "Organisation", pages: [Page("p1", [field])]);
        var formData = new Dictionary<string, object> { ["trust"] = "x" };

        _formatting.GetFieldValue("trust", formData).Returns("x");
        _formatting.HasFieldValue("trust", formData).Returns(true);
        _formatting.GetFormattedFieldValues("trust", formData).Returns([]);
        _formatting.IsFieldAllowMultiple("trust", Arg.Any<FormTemplate>()).Returns(false);

        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Single(rows);
        Assert.Equal("Trust", rows[0].Key);
        Assert.Equal(SummaryDisplayKind.NotAnswered, rows[0].Value.Kind);
    }

    [Fact]
    public void BuildPreview_regular_complex_multi_field_shows_header_only_without_formatted_values()
    {
        var field = Field("tags", "complexField", "Tags", complexFieldId: "tag-field");
        var task = Task("t1", "Organisation", pages: [Page("p1", [field])]);
        var formData = new Dictionary<string, object> { ["tags"] = "x" };

        _formatting.GetFieldValue("tags", formData).Returns("x");
        _formatting.HasFieldValue("tags", formData).Returns(true);
        _formatting.GetFormattedFieldValues("tags", formData).Returns([]);
        _formatting.IsFieldAllowMultiple("tags", Arg.Any<FormTemplate>()).Returns(true);

        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Single(rows);
        Assert.Equal(SummaryDisplayKind.NotAnswered, rows[0].Value.Kind);
    }

    [Fact]
    public void BuildPreview_regular_complex_multi_upload_lists_every_file()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var uploadJson = JsonSerializer.Serialize(new[]
        {
            new UploadDto { Id = firstId, OriginalFileName = "a.pdf" },
            new UploadDto { Id = secondId, OriginalFileName = "b.pdf" }
        });
        var field = Field("files", "upload", "Evidence");
        var task = Task("t1", "Evidence", pages: [Page("p1", [field])]);
        var formData = new Dictionary<string, object> { ["files"] = uploadJson };

        _formatting.GetFieldValue("files", formData).Returns(uploadJson);
        _formatting.HasFieldValue("files", formData).Returns(true);
        _formatting.GetFormattedFieldValues("files", formData).Returns(["a.pdf", "b.pdf"]);
        _formatting.GetFieldItemLabel("files", Arg.Any<FormTemplate>()).Returns("File");
        _formatting.IsFieldAllowMultiple("files", Arg.Any<FormTemplate>()).Returns(true);

        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal(3, rows.Count);
        Assert.Equal(SummaryDisplayKind.Empty, rows[0].Value.Kind);
        Assert.Equal("File 1", rows[1].Key);
        Assert.Equal(firstId, rows[1].Value.Files.Single().FileId);
        Assert.False(rows[1].Value.WrapFilesInDivs);
        Assert.Equal("File 2", rows[2].Key);
        Assert.Equal(secondId, rows[2].Value.Files.Single().FileId);
        Assert.Equal("t1", rows[2].Value.Files.Single().TaskId);
        Assert.Null(rows[2].Value.Files.Single().PageId);
    }

    [Fact]
    public void BuildPreview_regular_complex_upload_falls_back_when_json_cannot_be_parsed()
    {
        var field = Field("files", "upload", "Evidence");
        var task = Task("t1", "Evidence", pages: [Page("p1", [field])]);
        var formData = new Dictionary<string, object> { ["files"] = MalformedUploadJson };

        _formatting.GetFieldValue("files", formData).Returns(MalformedUploadJson);
        _formatting.HasFieldValue("files", formData).Returns(true);
        _formatting.GetFormattedFieldValues("files", formData).Returns(["fallback.pdf"]);
        _formatting.IsFieldAllowMultiple("files", Arg.Any<FormTemplate>()).Returns(false);

        var value = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows.Single().Value;

        Assert.Equal(SummaryDisplayKind.Html, value.Kind);
        Assert.Equal("fallback.pdf", value.Html);
    }

    [Fact]
    public void BuildPreview_regular_complex_multi_upload_falls_back_to_values_when_json_cannot_be_parsed()
    {
        var field = Field("files", "upload", "Evidence");
        var task = Task("t1", "Evidence", pages: [Page("p1", [field])]);
        var formData = new Dictionary<string, object> { ["files"] = MalformedUploadJson };

        _formatting.GetFieldValue("files", formData).Returns(MalformedUploadJson);
        _formatting.HasFieldValue("files", formData).Returns(true);
        _formatting.GetFormattedFieldValues("files", formData).Returns(["one.pdf", "two.pdf"]);
        _formatting.GetFieldItemLabel("files", Arg.Any<FormTemplate>()).Returns("File");
        _formatting.IsFieldAllowMultiple("files", Arg.Any<FormTemplate>()).Returns(true);

        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal(3, rows.Count);
        Assert.Equal("File 1", rows[1].Key);
        Assert.Equal("one.pdf", rows[1].Value.Html);
        Assert.Equal("File 2", rows[2].Key);
        Assert.Equal("two.pdf", rows[2].Value.Html);
    }

    [Fact]
    public void BuildPreview_uses_derived_rows_when_task_has_derived_flows_without_derived_mode()
    {
        var derived = DerivedFlow("sign", "Declarations", pages: [Page("p1", [Field("chair", "text", "Chair")])]);
        var task = Task("t1", "Sign", derivedFlows: [derived]);

        _derivedFlows.GenerateItemsFromSourceField("orgs", Arg.Any<Dictionary<string, object>>(), derived)
            .Returns([new DerivedCollectionItem { Id = "d1", DisplayName = "Trust A" }]);
        _derivedFlows.GetItemStatuses("decls", Arg.Any<Dictionary<string, object>>())
            .Returns(new Dictionary<string, string>());
        _derivedFlows.GetItemDeclarationData("decls", "d1", Arg.Any<Dictionary<string, object>>())
            .Returns(new Dictionary<string, object>());

        var rows = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal("Declarations", rows[0].Key);
        Assert.Equal("Trust A", rows[1].Key);
        Assert.Equal("Not signed yet", rows[1].Value.StatusText);
        Assert.False(rows[1].Value.StatusIsSigned);
        Assert.Equal("Chair", rows[2].Key);
        Assert.Equal(SummaryDisplayKind.NotAnswered, rows[2].Value.Kind);
    }

    [Fact]
    public void BuildPreview_derived_mode_without_configured_flows_produces_no_rows()
    {
        var task = Task("t1", "Sign", mode: FormStepPolicy.DerivedCollectionFlowMode);

        var rows = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Empty(rows);
    }

    [Fact]
    public void BuildPreview_derived_flows_are_ordered_by_section_order_and_use_default_empty_message()
    {
        var second = DerivedFlow("b", "Second section", fieldId: "declsB", sectionOrder: 2);
        var first = DerivedFlow("a", "First section", fieldId: "declsA", sectionOrder: 1);
        var task = Task(
            "t1",
            "Sign",
            mode: FormStepPolicy.DerivedCollectionFlowMode,
            derivedFlows: [second, first]);

        _derivedFlows.GenerateItemsFromSourceField(
                "orgs",
                Arg.Any<Dictionary<string, object>>(),
                Arg.Any<DerivedCollectionFlowConfiguration>())
            .Returns([]);

        var rows = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal(4, rows.Count);
        Assert.Equal("First section", rows[0].Key);
        Assert.Equal("No items", rows[1].Key);
        Assert.Contains("No items to display", rows[1].Value.Html);
        Assert.Equal("Second section", rows[2].Key);
    }

    [Fact]
    public void BuildPreview_derived_flow_handles_null_pages()
    {
        var derived = DerivedFlow("sign", "Declarations");
        derived.Pages = null!;
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
            .Returns(new Dictionary<string, object>());

        var rows = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal(2, rows.Count);
        Assert.Equal("Trust A", rows[1].Key);
        Assert.True(rows[1].Value.StatusIsSigned);
    }

    [Fact]
    public void BuildPreview_derived_flow_treats_null_declaration_value_as_not_answered()
    {
        var derived = DerivedFlow("sign", "Declarations", pages: [Page("p1", [Field("chair", "text", "Chair")])]);
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
            .Returns(new Dictionary<string, object> { ["chair"] = null! });

        var rows = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal("Chair", rows[2].Key);
        Assert.Equal(SummaryDisplayKind.NotAnswered, rows[2].Value.Kind);
    }

    [Fact]
    public void BuildPreview_derived_flow_renders_upload_json_as_files()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var uploadJson = JsonSerializer.Serialize(new[]
        {
            new UploadDto { Id = firstId, OriginalFileName = "a.pdf" },
            new UploadDto { Id = secondId, OriginalFileName = "b.pdf" }
        });
        var derived = DerivedFlow(
            "sign",
            "Declarations",
            pages: [Page("p1", [Field("evidence", "upload", "Evidence")])]);
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
            .Returns(new Dictionary<string, object> { ["evidence"] = uploadJson });

        var value = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows[2].Value;

        Assert.Equal(SummaryDisplayKind.UploadFiles, value.Kind);
        Assert.Equal(new[] { firstId, secondId }, value.Files.Select(f => f.FileId).ToArray());
        Assert.True(value.WrapFilesInDivs);
        _infectedFilter.DidNotReceiveWithAnyArgs().FilterList(default!, default);
    }

    [Fact]
    public void BuildPreview_derived_flow_falls_back_to_raw_value_when_options_are_missing()
    {
        var derived = DerivedFlow(
            "sign",
            "Declarations",
            pages:
            [
                Page("p1",
                [
                    Field("choice", "radios", "Choice"),
                    Field("region", "select", "Region"),
                    Field("tags", "checkboxes", "Tags")
                ])
            ]);
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
            .Returns(new Dictionary<string, object>
            {
                ["choice"] = "yes",
                ["region"] = "ne",
                ["tags"] = "a"
            });

        var rows = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal("yes", rows[2].Value.Html);
        Assert.Equal("ne", rows[3].Value.Html);
        Assert.Equal("a", rows[4].Value.Html);
        Assert.All(rows.Skip(2), row => Assert.Equal(SummaryDisplayKind.Html, row.Value.Kind));
    }

    [Fact]
    public void BuildPreview_derived_flow_uses_raw_value_when_option_is_not_matched()
    {
        var derived = DerivedFlow(
            "sign",
            "Declarations",
            pages:
            [
                Page("p1",
                [
                    Field("choice", "radios", "Choice", [new Option { Value = "yes", Label = "Yes" }]),
                    Field("region", "select", "Region", [new Option { Value = "ne", Label = "North East" }])
                ])
            ]);
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
            .Returns(new Dictionary<string, object> { ["choice"] = "maybe", ["region"] = "sw" });

        var rows = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal("maybe", rows[2].Value.Html);
        Assert.Equal("sw", rows[3].Value.Html);
    }

    [Fact]
    public void BuildPreview_derived_flow_checkboxes_fall_back_when_no_option_matches()
    {
        var derived = DerivedFlow(
            "sign",
            "Declarations",
            pages: [Page("p1", [Field("tags", "checkboxes", "Tags", [new Option { Value = "a", Label = "Tag A" }])])]);
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
            .Returns(new Dictionary<string, object> { ["tags"] = "unknown" });
        _formatting.GetFormattedFieldValues("tags", Arg.Any<Dictionary<string, object>>())
            .Returns(["unknown"]);

        var value = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows[2].Value;

        Assert.Equal(SummaryDisplayKind.Html, value.Kind);
        Assert.Equal("unknown", value.Html);
    }

    [Fact]
    public void BuildPreview_derived_flow_checkboxes_use_option_value_when_label_is_missing()
    {
        var derived = DerivedFlow(
            "sign",
            "Declarations",
            pages: [Page("p1", [Field("tags", "checkboxes", "Tags", [new Option { Value = "a", Label = null! }])])]);
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
            .Returns(new Dictionary<string, object> { ["tags"] = "a" });
        _formatting.GetFormattedFieldValues("tags", Arg.Any<Dictionary<string, object>>())
            .Returns(["a"]);

        var value = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows[2].Value;

        Assert.Equal(SummaryDisplayKind.Checkboxes, value.Kind);
        Assert.Equal("a", value.Checkboxes.Single());
    }

    [Fact]
    public void BuildPreview_collection_mode_without_flows_produces_no_rows()
    {
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode);

        var rows = _composer.BuildPreview(Context(new Dictionary<string, object>(), Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Empty(rows);
    }

    [Fact]
    public void BuildPreview_collection_flow_adds_only_item_headers_when_summary_columns_are_missing()
    {
        var flow = CollectionFlow("members", "memberList", "Members", summaryColumns: null);
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData("memberList", new Dictionary<string, object> { ["id"] = "i1" });

        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal(2, rows.Count);
        Assert.Equal("Members", rows[0].Key);
        Assert.Equal("Item 1", rows[1].Key);
    }

    [Fact]
    public void BuildPreview_collection_flow_shows_not_answered_for_unresolved_column()
    {
        var flow = CollectionFlow(
            "members",
            "memberList",
            "Members",
            summaryColumns: [new FlowSummaryColumn { Field = "role", Label = "Role" }]);
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "memberList",
            new Dictionary<string, object> { ["id"] = "i1", ["fullName"] = "Ada" });

        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal("Role", rows[2].Key);
        Assert.Equal(SummaryDisplayKind.NotAnswered, rows[2].Value.Kind);
    }

    [Fact]
    public void BuildPreview_collection_flow_shows_not_answered_when_formatting_returns_no_values()
    {
        var flow = CollectionFlow(
            "members",
            "memberList",
            "Members",
            summaryColumns: [new FlowSummaryColumn { Field = "role", Label = "Role" }]);
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "memberList",
            new Dictionary<string, object> { ["id"] = "i1", ["role"] = "Lead" });

        _formatting.GetFormattedFieldValues("role", Arg.Any<Dictionary<string, object>>()).Returns([]);

        var rows = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows;

        Assert.Equal(SummaryDisplayKind.NotAnswered, rows[2].Value.Kind);
    }

    [Fact]
    public void BuildPreview_collection_flow_renders_multiple_values_as_html_list()
    {
        var flow = CollectionFlow(
            "members",
            "memberList",
            "Members",
            summaryColumns: [new FlowSummaryColumn { Field = "role", Label = "Role" }]);
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "memberList",
            new Dictionary<string, object> { ["id"] = "i1", ["role"] = "Lead" });

        _formatting.GetFormattedFieldValues("role", Arg.Any<Dictionary<string, object>>())
            .Returns(["Lead", "Deputy"]);

        var value = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows[2].Value;

        Assert.Equal(SummaryDisplayKind.HtmlList, value.Kind);
        Assert.Equal(new[] { "Lead", "Deputy" }, value.HtmlItems.ToArray());
    }

    [Fact]
    public void BuildPreview_collection_flow_renders_single_upload_as_one_file_link()
    {
        var fileId = Guid.NewGuid();
        var uploadJson = JsonSerializer.Serialize(new[]
        {
            new UploadDto { Id = fileId, OriginalFileName = "evidence.pdf" }
        });
        var flow = CollectionFlow(
            "docs",
            "docList",
            "Documents",
            summaryColumns: [new FlowSummaryColumn { Field = "files", Label = "Files" }]);
        var task = Task("t1", "Docs", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "docList",
            new Dictionary<string, object> { ["id"] = "i1", ["files"] = uploadJson });

        _formatting.GetFormattedFieldValues("files", Arg.Any<Dictionary<string, object>>())
            .Returns(["evidence.pdf"]);

        var value = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows[2].Value;

        Assert.Equal(SummaryDisplayKind.UploadFiles, value.Kind);
        Assert.Equal(fileId, value.Files.Single().FileId);
        Assert.False(value.WrapFilesInDivs);
        _infectedFilter.DidNotReceiveWithAnyArgs().FilterList(default!, default);
    }

    [Fact]
    public void BuildPreview_collection_flow_renders_multiple_uploads_as_file_list()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var uploadJson = JsonSerializer.Serialize(new[]
        {
            new UploadDto { Id = firstId, OriginalFileName = "a.pdf" },
            new UploadDto { Id = secondId, OriginalFileName = "b.pdf" }
        });
        var flow = CollectionFlow(
            "docs",
            "docList",
            "Documents",
            summaryColumns: [new FlowSummaryColumn { Field = "files", Label = "Files" }]);
        var task = Task("t1", "Docs", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "docList",
            new Dictionary<string, object> { ["id"] = "i1", ["files"] = uploadJson });

        _formatting.GetFormattedFieldValues("files", Arg.Any<Dictionary<string, object>>())
            .Returns(["a.pdf", "b.pdf"]);

        var value = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows[2].Value;

        Assert.Equal(SummaryDisplayKind.UploadFiles, value.Kind);
        Assert.Equal(new[] { firstId, secondId }, value.Files.Select(f => f.FileId).ToArray());
        Assert.True(value.WrapFilesInDivs);
    }

    [Fact]
    public void BuildPreview_collection_flow_falls_back_to_html_list_when_upload_json_cannot_be_parsed()
    {
        var flow = CollectionFlow(
            "docs",
            "docList",
            "Documents",
            summaryColumns: [new FlowSummaryColumn { Field = "files", Label = "Files" }]);
        var task = Task("t1", "Docs", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "docList",
            new Dictionary<string, object> { ["id"] = "i1", ["files"] = MalformedUploadJson });

        _formatting.GetFormattedFieldValues("files", Arg.Any<Dictionary<string, object>>())
            .Returns(["a.pdf", "b.pdf"]);

        var value = _composer.BuildPreview(Context(formData, Template(task)))
            .Groups.Single().Tasks.Single().Rows[2].Value;

        Assert.Equal(SummaryDisplayKind.HtmlList, value.Kind);
        Assert.Equal(new[] { "a.pdf", "b.pdf" }, value.HtmlItems.ToArray());
    }

    [Fact]
    public void BuildCollectionFlows_returns_empty_when_task_summary_has_no_flow_list()
    {
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode);

        var sections = _composer.BuildCollectionFlows(
            Context(new Dictionary<string, object>(), Template(task), taskId: "t1"),
            task);

        Assert.Empty(sections);
    }

    [Fact]
    public void BuildCollectionFlows_renders_description_and_default_labels_and_allows_more_items()
    {
        var flow = CollectionFlow(
            "members",
            "memberList",
            "Members",
            summaryColumns: null,
            pages: [Page("p1", [Field("fullName", "text", "Name")])]);
        flow.Description = "Add each member";
        flow.MaxItems = 5;
        flow.TableType = null!;
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "memberList",
            new Dictionary<string, object> { ["id"] = "i1", ["fullName"] = "Ada" });

        var section = _composer
            .BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single();

        Assert.NotNull(section.DescriptionHtml);
        Assert.Contains("Add each member", section.DescriptionHtml);
        Assert.Equal("Item", section.ItemKind);
        Assert.Equal("Items", section.ItemKindPlural);
        Assert.Equal("Add item", section.AddButtonLabel);
        Assert.Equal("members-add-item", section.AddButtonId);
        Assert.Equal("members-no-items-added-hint", section.NoItemsHintId);
        Assert.StartsWith("/applications/REF-1/t1/flow/members/", section.AddUrl);
        Assert.True(section.CanAddMore);
        Assert.False(section.IsListStyle);
        Assert.Equal("Item 1", section.Items.Single().Title);
        Assert.Empty(section.Items.Single().Rows);
        Assert.False(section.Items.Single().HeaderRow.ShowSeparator);
    }

    [Fact]
    public void BuildCollectionFlows_handles_items_without_an_id()
    {
        var flow = CollectionFlow(
            "members",
            "memberList",
            "Members",
            summaryColumns: [new FlowSummaryColumn { Field = "fullName", Label = "Full name" }],
            pages: [Page("p1", [Field("fullName", "text", "Name")])]);
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData("memberList", new Dictionary<string, object> { ["fullName"] = "Ada" });

        _formatting.GetFormattedFieldValues("fullName", Arg.Any<Dictionary<string, object>>())
            .Returns(["Ada"]);

        var item = _composer
            .BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single().Items.Single();

        Assert.Equal(string.Empty, item.ItemId);
        Assert.Equal(string.Empty, item.Remove.ItemId);
        Assert.Equal("/applications/REF-1/t1/flow/members//p1", item.Rows.Single().ChangeUrl);
    }

    [Fact]
    public void BuildCollectionFlows_uses_empty_page_id_when_flow_has_no_pages()
    {
        var flow = CollectionFlow(
            "members",
            "memberList",
            "Members",
            summaryColumns: [new FlowSummaryColumn { Field = "fullName", Label = "Full name" }]);
        flow.Pages = null!;
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "memberList",
            new Dictionary<string, object> { ["id"] = "i1", ["fullName"] = "Ada" });

        _formatting.GetFormattedFieldValues("fullName", Arg.Any<Dictionary<string, object>>())
            .Returns(["Ada"]);

        var row = _composer
            .BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single().Items.Single().Rows.Single();

        Assert.Equal("/applications/REF-1/t1/flow/members/i1/", row.ChangeUrl);
        Assert.Equal("Full name for Item 1", row.ChangeHiddenText);
    }

    [Fact]
    public void BuildCollectionFlows_uses_first_page_when_column_field_is_not_configured()
    {
        var flow = CollectionFlow(
            "members",
            "memberList",
            "Members",
            summaryColumns: [new FlowSummaryColumn { Field = "fullName", Label = "Full name" }],
            pages: [Page("p1", [Field("other", "text", "Other")])]);
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "memberList",
            new Dictionary<string, object> { ["id"] = "i1", ["fullName"] = "Ada" });

        _formatting.GetFormattedFieldValues("fullName", Arg.Any<Dictionary<string, object>>())
            .Returns(["Ada"]);

        var row = _composer
            .BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single().Items.Single().Rows.Single();

        Assert.Equal("/applications/REF-1/t1/flow/members/i1/p1", row.ChangeUrl);
    }

    [Fact]
    public void BuildCollectionFlows_shows_not_answered_when_item_has_no_value_for_column()
    {
        var flow = CollectionFlow(
            "members",
            "memberList",
            "Members",
            summaryColumns: [new FlowSummaryColumn { Field = "fullName", Label = "Full name" }],
            pages: [Page("p1", [Field("fullName", "text", "Name")])]);
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData("memberList", new Dictionary<string, object> { ["id"] = "i1" });

        var row = _composer
            .BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single().Items.Single().Rows.Single();

        Assert.Equal(SummaryDisplayKind.NotAnswered, row.Value.Kind);
    }

    [Fact]
    public void BuildCollectionFlows_shows_not_answered_when_formatting_returns_no_values()
    {
        var flow = CollectionFlow(
            "members",
            "memberList",
            "Members",
            summaryColumns: [new FlowSummaryColumn { Field = "fullName", Label = "Full name" }],
            pages: [Page("p1", [Field("fullName", "text", "Name")])]);
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "memberList",
            new Dictionary<string, object> { ["id"] = "i1", ["fullName"] = "Ada" });

        _formatting.GetFormattedFieldValues("fullName", Arg.Any<Dictionary<string, object>>()).Returns([]);

        var row = _composer
            .BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single().Items.Single().Rows.Single();

        Assert.Equal(SummaryDisplayKind.NotAnswered, row.Value.Kind);
    }

    [Fact]
    public void BuildCollectionFlows_infers_autocomplete_json_from_other_item_fields()
    {
        var autocompleteField = Field("org", "complexField", "Organisation", complexFieldId: "org-auto");
        _complexFields.GetConfiguration("org-auto")
            .Returns(new ComplexFieldConfiguration { Id = "org-auto", FieldType = "autocomplete" });

        var flow = CollectionFlow(
            "members",
            "memberList",
            "Members",
            summaryColumns: [new FlowSummaryColumn { Field = "org", Label = "Organisation" }],
            pages: [Page("p1", [autocompleteField])]);
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "memberList",
            new Dictionary<string, object>
            {
                ["id"] = "i1",
                ["org"] = string.Empty,
                ["orgDetails"] = """{"name":"Inferred Trust"}"""
            });

        _formatting.GetFormattedFieldValues("org", Arg.Any<Dictionary<string, object>>())
            .Returns(call => [call.Arg<Dictionary<string, object>>()["org"].ToString() ?? string.Empty]);

        var value = _composer
            .BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single().Items.Single().Rows.Single().Value;

        Assert.Equal(SummaryDisplayKind.AutocompleteHtml, value.Kind);
        Assert.Contains("Inferred Trust", value.Html);
    }

    [Fact]
    public void BuildCollectionFlows_shows_not_answered_when_no_autocomplete_json_can_be_inferred()
    {
        var autocompleteField = Field("org", "complexField", "Organisation", complexFieldId: "org-auto");
        _complexFields.GetConfiguration("org-auto")
            .Returns(new ComplexFieldConfiguration { Id = "org-auto", FieldType = "autocomplete" });

        var flow = CollectionFlow(
            "members",
            "memberList",
            "Members",
            summaryColumns: [new FlowSummaryColumn { Field = "org", Label = "Organisation" }],
            pages: [Page("p1", [autocompleteField])]);
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "memberList",
            new Dictionary<string, object> { ["id"] = "i1", ["org"] = string.Empty });

        var value = _composer
            .BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single().Items.Single().Rows.Single().Value;

        Assert.Equal(SummaryDisplayKind.NotAnswered, value.Kind);
    }

    [Fact]
    public void BuildCollectionFlows_list_style_falls_back_when_checkbox_values_normalise_to_empty()
    {
        var checkboxField = Field("roles", "checkboxes", "Roles", [new Option { Value = "lead", Label = "Lead" }]);
        var flow = CollectionFlow(
            "members",
            "memberList",
            "Members",
            summaryColumns: [new FlowSummaryColumn { Field = "roles", Label = "Roles" }],
            pages: [Page("p1", [checkboxField])]);
        flow.TableType = "list";
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "memberList",
            new Dictionary<string, object> { ["id"] = "i1", ["roles"] = Array.Empty<string>() });

        _formatting.GetFormattedFieldValues("roles", Arg.Any<Dictionary<string, object>>())
            .Returns(["Fallback"]);

        var value = _composer
            .BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single().Items.Single().Rows.Single().Value;

        Assert.Equal(SummaryDisplayKind.Html, value.Kind);
        Assert.Equal("Fallback", value.Html);
    }

    [Fact]
    public void BuildCollectionFlows_renders_single_upload_as_one_file_link()
    {
        var uploadField = Field("files", "complexField", "Files", complexFieldId: "upload-1");
        _complexFields.GetConfiguration("upload-1")
            .Returns(new ComplexFieldConfiguration { Id = "upload-1", FieldType = "upload" });

        var fileId = Guid.NewGuid();
        var uploadJson = JsonSerializer.Serialize(new[]
        {
            new UploadDto { Id = fileId, OriginalFileName = "ok.pdf" }
        });
        var flow = CollectionFlow(
            "docs",
            "docList",
            "Documents",
            summaryColumns: [new FlowSummaryColumn { Field = "files", Label = "Files" }],
            pages: [Page("p1", [uploadField])]);
        var task = Task("t1", "Docs", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "docList",
            new Dictionary<string, object> { ["id"] = "i1", ["files"] = uploadJson });

        _formatting.GetFormattedFieldValues("files", Arg.Any<Dictionary<string, object>>())
            .Returns(["ok.pdf"]);

        var value = _composer
            .BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single().Items.Single().Rows.Single().Value;

        Assert.Equal(SummaryDisplayKind.UploadFiles, value.Kind);
        Assert.Equal(fileId, value.Files.Single().FileId);
        Assert.Equal("p1", value.Files.Single().PageId);
        Assert.False(value.WrapFilesInDivs);
        _infectedFilter.Received().FilterList(Arg.Any<IReadOnlyList<UploadDto>>(), "app-1");
    }

    [Fact]
    public void BuildCollectionFlows_uses_html_fallback_when_the_only_upload_is_filtered_out()
    {
        var uploadField = Field("files", "complexField", "Files", complexFieldId: "upload-1");
        _complexFields.GetConfiguration("upload-1")
            .Returns(new ComplexFieldConfiguration { Id = "upload-1", FieldType = "upload" });

        var uploadJson = JsonSerializer.Serialize(new[]
        {
            new UploadDto { Id = Guid.NewGuid(), OriginalFileName = "bad.pdf" }
        });
        var flow = CollectionFlow(
            "docs",
            "docList",
            "Documents",
            summaryColumns: [new FlowSummaryColumn { Field = "files", Label = "Files" }],
            pages: [Page("p1", [uploadField])]);
        var task = Task("t1", "Docs", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "docList",
            new Dictionary<string, object> { ["id"] = "i1", ["files"] = uploadJson });

        _formatting.GetFormattedFieldValues("files", Arg.Any<Dictionary<string, object>>())
            .Returns(["bad.pdf"]);
        _infectedFilter.FilterList(Arg.Any<IReadOnlyList<UploadDto>>(), Arg.Any<string?>())
            .Returns([]);

        var value = _composer
            .BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single().Items.Single().Rows.Single().Value;

        Assert.Equal(SummaryDisplayKind.Html, value.Kind);
        Assert.Equal("bad.pdf", value.Html);
    }

    [Fact]
    public void BuildCollectionFlows_uses_html_list_fallback_when_all_uploads_are_filtered_out()
    {
        var uploadField = Field("files", "complexField", "Files", complexFieldId: "upload-1");
        _complexFields.GetConfiguration("upload-1")
            .Returns(new ComplexFieldConfiguration { Id = "upload-1", FieldType = "upload" });

        var uploadJson = JsonSerializer.Serialize(new[]
        {
            new UploadDto { Id = Guid.NewGuid(), OriginalFileName = "bad1.pdf" },
            new UploadDto { Id = Guid.NewGuid(), OriginalFileName = "bad2.pdf" }
        });
        var flow = CollectionFlow(
            "docs",
            "docList",
            "Documents",
            summaryColumns: [new FlowSummaryColumn { Field = "files", Label = "Files" }],
            pages: [Page("p1", [uploadField])]);
        var task = Task("t1", "Docs", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "docList",
            new Dictionary<string, object> { ["id"] = "i1", ["files"] = uploadJson });

        _formatting.GetFormattedFieldValues("files", Arg.Any<Dictionary<string, object>>())
            .Returns(["bad1.pdf", "bad2.pdf"]);
        _infectedFilter.FilterList(Arg.Any<IReadOnlyList<UploadDto>>(), Arg.Any<string?>())
            .Returns([]);

        var value = _composer
            .BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single().Items.Single().Rows.Single().Value;

        Assert.Equal(SummaryDisplayKind.HtmlList, value.Kind);
        Assert.Equal(new[] { "bad1.pdf", "bad2.pdf" }, value.HtmlItems.ToArray());
    }

    [Fact]
    public void BuildCollectionFlows_uses_fallback_when_configured_upload_field_holds_an_empty_array()
    {
        var uploadField = Field("files", "complexField", "Files", complexFieldId: "upload-1");
        _complexFields.GetConfiguration("upload-1")
            .Returns(new ComplexFieldConfiguration { Id = "upload-1", FieldType = "upload" });

        var flow = CollectionFlow(
            "docs",
            "docList",
            "Documents",
            summaryColumns: [new FlowSummaryColumn { Field = "files", Label = "Files" }],
            pages: [Page("p1", [uploadField])]);
        var task = Task("t1", "Docs", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);
        var formData = ItemsFormData(
            "docList",
            new Dictionary<string, object> { ["id"] = "i1", ["files"] = Array.Empty<string>() });

        _formatting.GetFormattedFieldValues("files", Arg.Any<Dictionary<string, object>>())
            .Returns(["no files"]);

        var value = _composer
            .BuildCollectionFlows(Context(formData, Template(task), taskId: "t1"), task)
            .Single().Items.Single().Rows.Single().Value;

        Assert.Equal(SummaryDisplayKind.Html, value.Kind);
        Assert.Equal("no files", value.Html);
    }

    [Fact]
    public void BuildCollectionFlows_returns_no_items_for_malformed_or_null_item_json()
    {
        var flow = CollectionFlow(
            "members",
            "memberList",
            "Members",
            summaryColumns: [new FlowSummaryColumn { Field = "fullName", Label = "Full name" }],
            pages: [Page("p1", [Field("fullName", "text", "Name")])]);
        var task = Task("t1", "Team", mode: FormStepPolicy.MultiCollectionFlowMode, flows: [flow]);

        var malformed = new Dictionary<string, object> { ["memberList"] = "not-json" };
        var jsonNull = new Dictionary<string, object> { ["memberList"] = "null" };

        Assert.Empty(_composer
            .BuildCollectionFlows(Context(malformed, Template(task), taskId: "t1"), task)
            .Single().Items);
        Assert.Empty(_composer
            .BuildCollectionFlows(Context(jsonNull, Template(task), taskId: "t1"), task)
            .Single().Items);
    }

    private const string MalformedUploadJson = """["id"]""";

    private static Dictionary<string, object> ItemsFormData(
        string fieldId,
        params Dictionary<string, object>[] items) =>
        new() { [fieldId] = JsonSerializer.Serialize(items) };

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

    private static FormTemplate TemplateWithGroups(params TaskGroup[] groups) =>
        new()
        {
            TemplateId = "tpl",
            TemplateName = "tpl",
            Description = "tpl",
            TaskGroups = [.. groups]
        };

    private static TaskGroup Group(string id, string name, int order, params TaskModel[] tasks) =>
        new()
        {
            GroupId = id,
            GroupName = name,
            GroupOrder = order,
            GroupStatus = "NotStarted",
            Tasks = [.. tasks]
        };

    private static TaskModel Task(
        string id,
        string name,
        string mode = "standard",
        List<Page>? pages = null,
        List<MultiCollectionFlowConfiguration>? flows = null,
        List<DerivedCollectionFlowConfiguration>? derivedFlows = null,
        int order = 1) =>
        new()
        {
            TaskId = id,
            TaskName = name,
            TaskOrder = order,
            TaskStatusString = "NotStarted",
            Pages = pages,
            Summary = new TaskSummaryConfiguration
            {
                Mode = mode,
                Flows = flows,
                DerivedFlows = derivedFlows
            }
        };

    private static MultiCollectionFlowConfiguration CollectionFlow(
        string flowId,
        string fieldId,
        string title,
        List<FlowSummaryColumn>? summaryColumns = null,
        List<Page>? pages = null) =>
        new()
        {
            FlowId = flowId,
            FieldId = fieldId,
            Title = title,
            SummaryColumns = summaryColumns,
            Pages = pages ?? []
        };

    private static DerivedCollectionFlowConfiguration DerivedFlow(
        string flowId,
        string title,
        string sourceFieldId = "orgs",
        string fieldId = "decls",
        int sectionOrder = 1,
        List<Page>? pages = null) =>
        new()
        {
            FlowId = flowId,
            Title = title,
            SourceFieldId = sourceFieldId,
            FieldId = fieldId,
            SectionOrder = sectionOrder,
            Pages = pages ?? []
        };

    private static Page Page(string id, List<Field> fields, int order = 1) =>
        new()
        {
            PageId = id,
            Slug = id,
            Title = id,
            Description = id,
            PageOrder = order,
            Fields = fields
        };

    private static Field Field(
        string id,
        string type,
        string label,
        List<Option>? options = null,
        string? complexFieldId = null,
        int order = 1) =>
        new()
        {
            FieldId = id,
            Type = type,
            Label = new Label { Value = label },
            Order = order,
            Options = options,
            ComplexField = complexFieldId == null ? null : new ComplexField { Id = complexFieldId }
        };
}
