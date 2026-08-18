using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;
using PageModel = GovUK.Dfe.FlexForms.Domain.Models.Page;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

internal static class FormEngineDerivedItems
{
    public static void LoadItemData(
        DerivedCollectionFlowConfiguration config,
        string itemId,
        FormEngineWorkState state,
        IDerivedCollectionFlowService derivedCollectionFlowService,
        ILogger logger)
    {
        try
        {
            var existingData = derivedCollectionFlowService.GetItemDeclarationData(config.FieldId, itemId, state.FormData);
            foreach (var kvp in existingData)
                state.Data[kvp.Key] = kvp.Value;

            var derivedItems = derivedCollectionFlowService.GenerateItemsFromSourceField(
                config.SourceFieldId,
                state.FormData,
                config);
            var currentItem = derivedItems.FirstOrDefault(item => item.Id == itemId);

            if (currentItem != null)
            {
                foreach (var kvp in currentItem.PrefilledData)
                {
                    if (!state.Data.ContainsKey(kvp.Key))
                        state.Data[kvp.Key] = kvp.Value;
                }

                logger.LogInformation(
                    "Loaded derived item data for item {ItemId} in flow {FlowId} with {Count} fields",
                    itemId,
                    config.FlowId,
                    currentItem.PrefilledData.Count);
            }

            if (state.CurrentPage != null)
            {
                foreach (var field in state.CurrentPage.Fields)
                {
                    if (field.Label != null)
                        field.Label.IsVisible = true;
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load derived item data for item {ItemId} in flow {FlowId}", itemId, config.FlowId);
        }
    }

    public static void ApplyDisplayNamePlaceholders(PageModel page, string displayName)
    {
        if (!string.IsNullOrEmpty(page.Title))
        {
            page.Title = page.Title
                .Replace("{displayName}", displayName)
                .Replace("{name}", displayName);
        }

        if (!string.IsNullOrEmpty(page.Description))
        {
            page.Description = page.Description
                .Replace("{displayName}", displayName)
                .Replace("{name}", displayName);
        }
    }

    public static string GetDisplayName(
        DerivedCollectionFlowConfiguration config,
        string itemId,
        Dictionary<string, object> formData,
        IDerivedCollectionFlowService derivedCollectionFlowService)
    {
        try
        {
            var items = derivedCollectionFlowService.GenerateItemsFromSourceField(config.SourceFieldId, formData, config);
            var match = items.FirstOrDefault(i => string.Equals(i.Id, itemId, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                if (!string.IsNullOrWhiteSpace(match.DisplayName))
                    return match.DisplayName;

                if (match.PrefilledData != null
                    && match.PrefilledData.TryGetValue(config.ItemTitleBinding, out var value)
                    && !string.IsNullOrWhiteSpace(value?.ToString()))
                {
                    return value.ToString()!;
                }
            }
        }
        catch (Exception)
        {
            // Fall back to the raw item id when source data cannot be read.
        }

        return itemId;
    }
}
