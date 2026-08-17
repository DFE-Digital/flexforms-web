using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.FormEngine;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using GovUK.Dfe.FlexForms.Web.Pages.FormEngine;
using GovUK.Dfe.FlexForms.Web.Utilities;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

/// <summary>
/// Composes preview and collection-flow view models from template data and formatting services.
/// </summary>
public sealed class FormEnginePresentationComposer(
    IFieldFormattingService fieldFormattingService,
    IComplexFieldConfigurationService complexFieldConfigurationService,
    IInfectedUploadFilter infectedUploadFilter,
    IDerivedCollectionFlowService derivedCollectionFlowService) : IFormEnginePresentationComposer
{
    public ApplicationPreviewViewModel BuildPreview(FormEnginePresentationContext context)
    {
        var groups = context.Template.TaskGroups
            .OrderBy(g => g.GroupOrder)
            .Select(group => new PreviewGroupViewModel
            {
                GroupName = group.GroupName,
                TestId = ToTestId(group.GroupName),
                Tasks = group.Tasks
                    .OrderBy(t => t.TaskOrder)
                    .Select(task => BuildPreviewTask(context, task))
                    .ToList()
            })
            .ToList();

        return new ApplicationPreviewViewModel
        {
            ReferenceNumber = context.ReferenceNumber,
            Groups = groups,
            Submit = new PreviewSubmitViewModel
            {
                IsEditable = context.IsEditable,
                IsLeadApplicant = context.IsLeadApplicant,
                SubmitDisabledByConfig = context.SubmitDisabledByConfig,
                DisabledBannerText = context.SubmitDisabledBannerText,
                DisabledHelpText = context.SubmitDisabledHelpText,
                FileValidationBlocksSubmit = context.FileValidationBlocksSubmit,
                BlockingFiles = context.BlockingFiles,
                IncludePreviewQuery = context.IncludePreviewQuery
            }
        };
    }

    public IReadOnlyList<CollectionFlowSectionViewModel> BuildCollectionFlows(
        FormEnginePresentationContext context,
        TaskModel task)
    {
        var flows = task.Summary?.Flows;
        if (flows == null || flows.Count == 0)
            return [];

        return flows.Select(flow => BuildCollectionSection(context, task, flow)).ToList();
    }

    private PreviewTaskCardViewModel BuildPreviewTask(FormEnginePresentationContext context, TaskModel task)
    {
        var testId = ToTestId(task.TaskName);
        var changeUrl = $"/applications/{context.ReferenceNumber}/{task.TaskId}";
        IReadOnlyList<SummaryRowViewModel> rows;

        if (FormStepPolicy.IsDerivedCollectionFlowSummary(task)
            || (task.Summary?.DerivedFlows != null && task.Summary.DerivedFlows.Count > 0))
        {
            rows = BuildDerivedPreviewRows(context, task);
        }
        else if (FormStepPolicy.IsCollectionFlowSummary(task))
        {
            rows = BuildCollectionPreviewRows(context, task);
        }
        else
        {
            rows = BuildRegularPreviewRows(context, task);
        }

        return new PreviewTaskCardViewModel
        {
            TaskId = task.TaskId,
            TaskName = task.TaskName,
            TestId = testId,
            ChangeUrl = changeUrl,
            Rows = rows
        };
    }

    private List<SummaryRowViewModel> BuildDerivedPreviewRows(
        FormEnginePresentationContext context,
        TaskModel task)
    {
        var rows = new List<SummaryRowViewModel>();
        foreach (var derivedFlow in (task.Summary?.DerivedFlows ?? []).OrderBy(f => f.SectionOrder))
        {
            rows.Add(HeaderRow(derivedFlow.Title));

            var derivedItems = derivedCollectionFlowService.GenerateItemsFromSourceField(
                derivedFlow.SourceFieldId, context.FormData, derivedFlow);

            if (derivedItems.Count == 0)
            {
                rows.Add(new SummaryRowViewModel
                {
                    Key = "No items",
                    Value = SummaryValueViewModel.FromHtml(
                        $"<span class=\"govuk-hint\">{System.Net.WebUtility.HtmlEncode(derivedFlow.EmptyStateMessage ?? "No items to display")}</span>")
                });
                continue;
            }

            var statuses = derivedCollectionFlowService.GetItemStatuses(derivedFlow.FieldId, context.FormData);
            foreach (var item in derivedItems)
            {
                var declarationData = derivedCollectionFlowService.GetItemDeclarationData(
                    derivedFlow.FieldId, item.Id, context.FormData);
                var status = statuses.TryGetValue(item.Id, out var s) ? s : "Not signed yet";

                rows.Add(new SummaryRowViewModel
                {
                    Key = item.DisplayName,
                    KeyIsBold = true,
                    Value = SummaryValueViewModel.FromStatusTag(status)
                });

                foreach (var page in (derivedFlow.Pages ?? []).OrderBy(p => p.PageOrder))
                {
                    foreach (var field in page.Fields.OrderBy(f => f.Order))
                    {
                        var fieldValue = declarationData.TryGetValue(field.FieldId, out var v)
                            ? v?.ToString() ?? string.Empty
                            : string.Empty;
                        rows.Add(new SummaryRowViewModel
                        {
                            Key = field.Label.Value,
                            Value = BuildDerivedFieldValue(context, task, field, fieldValue)
                        });
                    }
                }
            }
        }

        return rows;
    }

    private SummaryValueViewModel BuildDerivedFieldValue(
        FormEnginePresentationContext context,
        TaskModel task,
        Field field,
        string fieldValue)
    {
        if (string.IsNullOrEmpty(fieldValue))
            return SummaryValueViewModel.NotAnswered;

        if (LooksLikeUploadJson(fieldValue))
        {
            return TryBuildUploadValue(
                fieldValue,
                [ToHtmlBreaks(fieldValue)],
                context,
                task.TaskId,
                pageId: null,
                filterInfected: false,
                showAllFiles: true,
                fallbackWhenEmpty: SummaryValueViewModel.FromHtml(ToHtmlBreaks(fieldValue)));
        }

        if (field.Type == "radios" && field.Options != null)
        {
            var selectedOption = field.Options.FirstOrDefault(o => o.Value == fieldValue);
            return SummaryValueViewModel.FromHtml(selectedOption?.Label ?? fieldValue);
        }

        if (field.Type == "select" && field.Options != null)
        {
            var selectedOption = field.Options.FirstOrDefault(o => o.Value == fieldValue);
            return SummaryValueViewModel.FromHtml(selectedOption?.Label ?? fieldValue);
        }

        if (field.Type == "checkboxes" && field.Options != null)
        {
            var selectedValues = fieldFormattingService.GetFormattedFieldValues(field.FieldId, context.FormData);
            var selectedLabels = field.Options
                .Where(o => selectedValues.Contains(o.Value))
                .Select(o => o.Label ?? o.Value)
                .ToList();

            return selectedLabels.Count > 0
                ? SummaryValueViewModel.FromCheckboxes(selectedLabels)
                : SummaryValueViewModel.FromHtml(ToHtmlBreaks(fieldValue));
        }

        return SummaryValueViewModel.FromHtml(ToHtmlBreaks(fieldValue));
    }

    private List<SummaryRowViewModel> BuildCollectionPreviewRows(
        FormEnginePresentationContext context,
        TaskModel task)
    {
        var rows = new List<SummaryRowViewModel>();
        foreach (var flow in task.Summary?.Flows ?? [])
        {
            rows.Add(HeaderRow(flow.Title));

            var items = DeserializeItems(context.FormData, flow.FieldId);
            if (items.Count == 0)
            {
                rows.Add(new SummaryRowViewModel
                {
                    Key = "No items added",
                    Value = SummaryValueViewModel.NotAnswered
                });
                continue;
            }

            var itemLabel = flow.ItemKind ?? "Item";
            var summaryColumns = flow.SummaryColumns ?? [];
            var itemIndex = 0;
            foreach (var item in items)
            {
                itemIndex++;
                var expandedItem = DisplayHelpers.ExpandEncodedJson(item) ?? item;
                context.EnsureItemFieldVisibility(item, summaryColumns.Select(c => c.Field));
                var displayTitle = !string.IsNullOrEmpty(flow.ItemTitleBinding)
                    ? DisplayHelpers.InterpolateMessage($"{{{flow.ItemTitleBinding}}}", expandedItem)
                    : $"{itemLabel} {itemIndex}";

                rows.Add(HeaderRow(displayTitle));

                foreach (var col in summaryColumns.Where(c => !context.IsFieldHiddenForItem(c.Field, item)))
                {
                    var rawValue = DisplayHelpers.InterpolateMessage($"{{{col.Field}}}", expandedItem);
                    var value = rawValue == $"{{{col.Field}}}" ? string.Empty : rawValue;
                    rows.Add(new SummaryRowViewModel
                    {
                        Key = col.Label,
                        Value = string.IsNullOrEmpty(value)
                            ? SummaryValueViewModel.NotAnswered
                            : BuildPreviewCollectionFieldValue(context, task, col.Field, value)
                    });
                }
            }
        }

        return rows;
    }

    private SummaryValueViewModel BuildPreviewCollectionFieldValue(
        FormEnginePresentationContext context,
        TaskModel task,
        string fieldId,
        string value)
    {
        var formattedValues = FormatWithItemValue(context.FormData, fieldId, value);
        var isUploadField = LooksLikeUploadJson(value);

        if (formattedValues.Count == 0)
            return SummaryValueViewModel.NotAnswered;

        if (formattedValues.Count == 1)
        {
            if (!isUploadField)
                return SummaryValueViewModel.FromHtml(formattedValues[0]);

            return TryBuildUploadValue(
                value,
                formattedValues,
                context,
                task.TaskId,
                pageId: null,
                filterInfected: false,
                showAllFiles: false,
                fallbackWhenEmpty: SummaryValueViewModel.FromHtml(formattedValues[0]));
        }

        if (!isUploadField)
            return SummaryValueViewModel.FromHtmlList(formattedValues);

        return TryBuildUploadValue(
            value,
            formattedValues,
            context,
            task.TaskId,
            pageId: null,
            filterInfected: false,
            showAllFiles: true,
            fallbackWhenEmpty: SummaryValueViewModel.FromHtmlList(formattedValues));
    }

    private List<SummaryRowViewModel> BuildRegularPreviewRows(
        FormEnginePresentationContext context,
        TaskModel task)
    {
        var rows = new List<SummaryRowViewModel>();
        foreach (var page in (task.Pages ?? []).OrderBy(p => p.PageOrder))
        {
            foreach (var field in page.Fields.OrderBy(f => f.Order).Where(f => !context.IsFieldHidden(f.FieldId)))
            {
                var fieldValue = fieldFormattingService.GetFieldValue(field.FieldId, context.FormData);
                var hasValue = fieldFormattingService.HasFieldValue(field.FieldId, context.FormData);

                if ((field.Type == "autocomplete" || field.Type == "complexField" || field.Type == "upload") && hasValue)
                {
                    rows.AddRange(BuildRegularComplexRows(context, task, field, fieldValue));
                }
                else
                {
                    rows.Add(new SummaryRowViewModel
                    {
                        Key = field.Label.Value,
                        Value = BuildRegularSimpleValue(context, task, field, fieldValue, hasValue)
                    });
                }
            }
        }

        return rows;
    }

    private List<SummaryRowViewModel> BuildRegularComplexRows(
        FormEnginePresentationContext context,
        TaskModel task,
        Field field,
        string fieldValue)
    {
        var formattedValues = fieldFormattingService.GetFormattedFieldValues(field.FieldId, context.FormData);
        var itemLabel = fieldFormattingService.GetFieldItemLabel(field.FieldId, context.Template);
        var allowMultiple = fieldFormattingService.IsFieldAllowMultiple(field.FieldId, context.Template);
        var isUploadField = LooksLikeUploadJson(fieldValue);
        var rows = new List<SummaryRowViewModel>();

        SummaryValueViewModel headerValue;
        if (formattedValues.Count == 0)
        {
            headerValue = SummaryValueViewModel.NotAnswered;
        }
        else if (!allowMultiple)
        {
            if (isUploadField)
            {
                headerValue = TryBuildUploadValue(
                    fieldValue,
                    formattedValues,
                    context,
                    task.TaskId,
                    pageId: null,
                    filterInfected: false,
                    showAllFiles: false,
                    fallbackWhenEmpty: SummaryValueViewModel.FromHtml(formattedValues.FirstOrDefault() ?? string.Empty));
            }
            else
            {
                var html = AutocompleteSummaryFormatter.Render(DisplayHelpers.UnsanitiseHtmlInput(fieldValue));
                headerValue = SummaryValueViewModel.FromAutocompleteHtml(html);
            }
        }
        else
        {
            headerValue = SummaryValueViewModel.Empty;
        }

        rows.Add(new SummaryRowViewModel
        {
            Key = field.Label.Value,
            Value = headerValue
        });

        if (!allowMultiple || formattedValues.Count == 0)
            return rows;

        if (isUploadField && TryParseUploads(fieldValue, out var uploadFiles) && uploadFiles.Count > 0)
        {
            for (var i = 0; i < uploadFiles.Count; i++)
            {
                var file = uploadFiles[i];
                rows.Add(new SummaryRowViewModel
                {
                    Key = $"{itemLabel} {i + 1}",
                    Value = SummaryValueViewModel.FromFiles(
                        [ToFileLink(file, context, task.TaskId, pageId: null)],
                        wrapFilesInDivs: false)
                });
            }

            return rows;
        }

        for (var i = 0; i < formattedValues.Count; i++)
        {
            rows.Add(new SummaryRowViewModel
            {
                Key = $"{itemLabel} {i + 1}",
                Value = SummaryValueViewModel.FromHtml(formattedValues[i])
            });
        }

        return rows;
    }

    private SummaryValueViewModel BuildRegularSimpleValue(
        FormEnginePresentationContext context,
        TaskModel task,
        Field field,
        string fieldValue,
        bool hasValue)
    {
        if (!hasValue)
            return SummaryValueViewModel.NotAnswered;

        if (LooksLikeUploadJson(fieldValue))
        {
            return TryBuildUploadValue(
                fieldValue,
                [ToHtmlBreaks(fieldValue)],
                context,
                task.TaskId,
                pageId: null,
                filterInfected: false,
                showAllFiles: false,
                fallbackWhenEmpty: SummaryValueViewModel.FromHtml(ToHtmlBreaks(fieldValue)));
        }

        if (field.Type == "radios" && field.Options != null)
        {
            var selectedOption = field.Options.FirstOrDefault(o => o.Value == fieldValue);
            return SummaryValueViewModel.FromText(selectedOption?.Label ?? fieldValue);
        }

        if (field.Type == "select" && field.Options != null)
        {
            var selectedOption = field.Options.FirstOrDefault(o => o.Value == fieldValue);
            return SummaryValueViewModel.FromText(selectedOption?.Label ?? fieldValue);
        }

        return SummaryValueViewModel.FromHtml(ToHtmlBreaks(fieldValue));
    }

    private CollectionFlowSectionViewModel BuildCollectionSection(
        FormEnginePresentationContext context,
        TaskModel task,
        MultiCollectionFlowConfiguration flow)
    {
        var items = DeserializeItems(context.FormData, flow.FieldId);
        var itemLabel = flow.ItemKind ?? "Item";
        var itemLabelPlural = flow.ItemKindPlural ?? $"{itemLabel}s";
        var isListStyle = flow.TableType?.Equals("list", StringComparison.OrdinalIgnoreCase) == true;
        var descriptionHtml = string.IsNullOrEmpty(flow.Description)
            ? null
            : MarkdownSafe.RenderHintWithClass(flow.Description).html;

        var itemVms = new List<CollectionFlowItemViewModel>();
        var index = 0;
        foreach (var item in items)
        {
            index++;
            var expandedItem = DisplayHelpers.ExpandEncodedJson(item) ?? item;
            var summaryColumns = flow.SummaryColumns ?? [];
            context.EnsureItemFieldVisibility(item, summaryColumns.Select(c => c.Field));
            var memberTitle = !string.IsNullOrEmpty(flow.ItemTitleBinding)
                ? DisplayHelpers.InterpolateMessage($"{{{flow.ItemTitleBinding}}}", expandedItem)
                : $"{itemLabel} {index}";
            var itemId = item.TryGetValue("id", out var idValue) ? idValue?.ToString() ?? string.Empty : string.Empty;

            var remove = new CollectionItemRemoveViewModel
            {
                ReferenceNumber = context.ReferenceNumber,
                TaskId = context.TaskId,
                FlowId = flow.FlowId,
                FieldId = flow.FieldId,
                ItemId = itemId,
                ItemTitle = memberTitle,
                TaskName = task.TaskName,
                ConfirmationTitle = $"Are you sure you want to remove this {itemLabel.ToLower()}?",
                RequiredMessage = $"Select yes if you are sure you want to remove this {itemLabel.ToLower()}",
                ButtonId = "remove-flow-item-@memberNumber"
            };

            itemVms.Add(new CollectionFlowItemViewModel
            {
                ItemId = itemId,
                Title = memberTitle,
                Remove = remove,
                HeaderRow = new SummaryRowViewModel
                {
                    Key = memberTitle,
                    KeyIsBold = true,
                    ShowSeparator = index > 1,
                    Value = SummaryValueViewModel.Empty,
                    Remove = remove
                },
                Rows = BuildCollectionItemRows(context, flow, item, isListStyle, memberTitle)
            });
        }

        return new CollectionFlowSectionViewModel
        {
            FlowId = flow.FlowId,
            Title = flow.Title,
            DescriptionHtml = descriptionHtml,
            ItemKind = itemLabel,
            ItemKindPlural = itemLabelPlural,
            AddButtonLabel = flow.AddButtonLabel,
            AddButtonId = flow.FlowId + "-add-item",
            AddUrl = $"/applications/{context.ReferenceNumber}/{context.TaskId}/flow/{flow.FlowId}/{Guid.NewGuid()}",
            NoItemsHintId = flow.FlowId + "-no-items-added-hint",
            CanAddMore = !flow.MaxItems.HasValue || items.Count < flow.MaxItems.Value,
            IsListStyle = isListStyle,
            Items = itemVms
        };
    }

    private List<SummaryRowViewModel> BuildCollectionItemRows(
        FormEnginePresentationContext context,
        MultiCollectionFlowConfiguration flow,
        Dictionary<string, object> item,
        bool isListStyle,
        string memberTitle)
    {
        var rows = new List<SummaryRowViewModel>();
        var summaryColumns = flow.SummaryColumns ?? [];
        foreach (var col in summaryColumns.Where(c => !context.IsFieldHiddenForItem(c.Field, item)))
        {
            var value = CoerceItemValue(item.TryGetValue(col.Field, out var v) ? v : null);
            var targetPage = flow.Pages?.FirstOrDefault(p => p.Fields.Any(f => f.FieldId == col.Field));
            var pageId = targetPage?.PageId ?? flow.Pages?.FirstOrDefault()?.PageId ?? string.Empty;
            var fieldConfig = targetPage?.Fields.FirstOrDefault(f => f.FieldId == col.Field);
            var (isAutocompleteField, isUploadFieldByConfig) = DetectComplexFieldTypes(fieldConfig);

            if (!isListStyle && isAutocompleteField && string.IsNullOrEmpty(value))
            {
                var inferred = AutocompleteSummaryFormatter.TryFindJsonInItem(item);
                if (!string.IsNullOrEmpty(inferred))
                    value = inferred;
            }

            var changeUrl = $"/applications/{context.ReferenceNumber}/{context.TaskId}/flow/{flow.FlowId}/{(item.TryGetValue("id", out var changeItemId) ? changeItemId?.ToString() : string.Empty)}/{pageId}";
            var changeHiddenText = $"{col.Label} for {memberTitle}";

            SummaryValueViewModel valueVm;
            if (string.IsNullOrEmpty(value))
            {
                valueVm = SummaryValueViewModel.NotAnswered;
            }
            else if (isListStyle && fieldConfig?.Type == "checkboxes")
            {
                var checkboxValues = CheckboxValueNormalizer.Normalize(
                    (item.TryGetValue(col.Field, out var valueObj) ? valueObj : null) ?? value);
                valueVm = checkboxValues.Count > 0
                    ? SummaryValueViewModel.FromCheckboxes(checkboxValues.ToList())
                    : BuildCollectionFormattedValue(
                        context, col.Field, value, item, isAutocompleteField, isUploadFieldByConfig, pageId, unsanitiseAutocomplete: false);
            }
            else
            {
                valueVm = BuildCollectionFormattedValue(
                    context,
                    col.Field,
                    value,
                    item,
                    isAutocompleteField,
                    isUploadFieldByConfig,
                    pageId,
                    unsanitiseAutocomplete: !isListStyle);
            }

            rows.Add(new SummaryRowViewModel
            {
                Key = col.Label,
                Value = valueVm,
                ChangeUrl = changeUrl,
                ChangeHiddenText = changeHiddenText
            });
        }

        return rows;
    }

    private SummaryValueViewModel BuildCollectionFormattedValue(
        FormEnginePresentationContext context,
        string fieldId,
        string value,
        Dictionary<string, object> item,
        bool isAutocompleteField,
        bool isUploadFieldByConfig,
        string pageId,
        bool unsanitiseAutocomplete)
    {
        var formattedValues = FormatWithItemValue(context.FormData, fieldId, value);

        if (isAutocompleteField && string.IsNullOrEmpty(value))
        {
            var inferred = AutocompleteSummaryFormatter.TryFindJsonInItem(item);
            if (!string.IsNullOrEmpty(inferred))
                value = inferred;
        }

        var isUploadField = isUploadFieldByConfig || LooksLikeUploadJson(value);

        if (formattedValues.Count == 0)
            return SummaryValueViewModel.NotAnswered;

        if (formattedValues.Count == 1)
        {
            if (isUploadField)
            {
                return TryBuildUploadValue(
                    value,
                    formattedValues,
                    context,
                    context.TaskId,
                    pageId,
                    filterInfected: true,
                    showAllFiles: false,
                    fallbackWhenEmpty: SummaryValueViewModel.FromHtml(formattedValues[0]));
            }

            if (isAutocompleteField)
            {
                var raw = unsanitiseAutocomplete ? DisplayHelpers.UnsanitiseHtmlInput(value) : value;
                return SummaryValueViewModel.FromAutocompleteHtml(AutocompleteSummaryFormatter.Render(raw));
            }

            return SummaryValueViewModel.FromHtml(formattedValues[0]);
        }

        if (isUploadField)
        {
            return TryBuildUploadValue(
                value,
                formattedValues,
                context,
                context.TaskId,
                pageId,
                filterInfected: true,
                showAllFiles: true,
                fallbackWhenEmpty: SummaryValueViewModel.FromHtmlList(formattedValues));
        }

        return SummaryValueViewModel.FromHtmlList(formattedValues);
    }

    private (bool IsAutocomplete, bool IsUpload) DetectComplexFieldTypes(Field? fieldConfig)
    {
        if (fieldConfig is not { Type: "complexField", ComplexField: not null })
            return (false, false);

        var cfg = complexFieldConfigurationService.GetConfiguration(fieldConfig.ComplexField.Id);
        return (
            string.Equals(cfg.FieldType, "autocomplete", StringComparison.OrdinalIgnoreCase),
            string.Equals(cfg.FieldType, "upload", StringComparison.OrdinalIgnoreCase));
    }

    private List<string> FormatWithItemValue(Dictionary<string, object> formData, string fieldId, string value)
    {
        var snapshot = new Dictionary<string, object>(formData) { [fieldId] = value };
        return fieldFormattingService.GetFormattedFieldValues(fieldId, snapshot);
    }

    private SummaryValueViewModel TryBuildUploadValue(
        string rawValue,
        IReadOnlyList<string> formattedValues,
        FormEnginePresentationContext context,
        string taskId,
        string? pageId,
        bool filterInfected,
        bool showAllFiles,
        SummaryValueViewModel fallbackWhenEmpty)
    {
        if (!TryParseUploads(rawValue, out var uploadFiles))
        {
            return showAllFiles && formattedValues.Count > 1
                ? SummaryValueViewModel.FromHtmlList(formattedValues)
                : SummaryValueViewModel.FromHtml(formattedValues.Count > 0 ? formattedValues[0] : string.Empty);
        }

        if (filterInfected)
            uploadFiles = infectedUploadFilter.FilterList(uploadFiles, context.InfectedFilterApplicationId);

        if (uploadFiles.Count == 0)
            return fallbackWhenEmpty;

        var files = showAllFiles
            ? uploadFiles.Select(f => ToFileLink(f, context, taskId, pageId)).ToList()
            : [ToFileLink(uploadFiles[0], context, taskId, pageId)];

        return SummaryValueViewModel.FromFiles(files, wrapFilesInDivs: showAllFiles);
    }

    private static SummaryFileLinkViewModel ToFileLink(
        UploadDto file,
        FormEnginePresentationContext context,
        string taskId,
        string? pageId) =>
        new()
        {
            FileId = file.Id,
            FileName = file.OriginalFileName,
            ReferenceNumber = context.ReferenceNumber,
            TaskId = taskId,
            ApplicationId = context.ApplicationId,
            PageId = pageId
        };

    private static SummaryRowViewModel HeaderRow(string title) =>
        new()
        {
            Key = title,
            KeyIsBold = true,
            Value = SummaryValueViewModel.Empty
        };

    private static List<Dictionary<string, object>> DeserializeItems(
        Dictionary<string, object> formData,
        string fieldId)
    {
        formData.TryGetValue(fieldId, out var raw);
        var json = raw?.ToString() ?? "[]";
        try
        {
            return JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string CoerceItemValue(object? valueObj) =>
        valueObj switch
        {
            string sv => sv,
            JsonElement je => je.ToString(),
            not null => JsonSerializer.Serialize(valueObj),
            _ => string.Empty
        };

    private static bool LooksLikeUploadJson(string? value) =>
        !string.IsNullOrEmpty(value) && value.StartsWith('[') && value.Contains("\"id\"");

    private static bool TryParseUploads(string value, out List<UploadDto> files)
    {
        try
        {
            files = JsonSerializer.Deserialize<List<UploadDto>>(value) ?? [];
            return true;
        }
        catch (JsonException)
        {
            files = [];
            return false;
        }
    }

    private static string ToHtmlBreaks(string value) =>
        value.Replace("\r\n", "<br/>").Replace("\r", "<br/>").Replace("\n", "<br/>");

    private static string ToTestId(string name) => name.Replace(" ", "-").ToLower();
}
