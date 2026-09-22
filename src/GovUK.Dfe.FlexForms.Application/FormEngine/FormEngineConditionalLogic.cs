using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

internal static class FormEngineConditionalLogic
{
    /// <summary>
    /// Merges current page data with saved answers so conditional rules still see earlier triggers.
    /// </summary>
    public static Dictionary<string, object> BuildEvaluationData(
        Dictionary<string, object> pageData,
        Dictionary<string, object> formData,
        Dictionary<string, object>? accumulated = null)
    {
        var dataForConditionalLogic = pageData.Count > 0
            ? new Dictionary<string, object>(pageData)
            : new Dictionary<string, object>(formData);

        if (accumulated != null)
        {
            foreach (var kvp in accumulated)
            {
                if (!dataForConditionalLogic.ContainsKey(kvp.Key))
                    dataForConditionalLogic[kvp.Key] = kvp.Value;
            }
        }

        foreach (var kvp in formData)
        {
            if (!dataForConditionalLogic.ContainsKey(kvp.Key))
                dataForConditionalLogic[kvp.Key] = kvp.Value;
        }

        return dataForConditionalLogic;
    }

    public static async Task<FormConditionalState> ApplyAsync(
        FormTemplate? template,
        Dictionary<string, object> data,
        Dictionary<string, object> formData,
        IConditionalLogicOrchestrator orchestrator,
        string pageId,
        string taskId,
        string trigger,
        ILogger logger,
        Dictionary<string, object>? accumulatedForChange = null)
    {
        try
        {
            if (template?.ConditionalLogic == null || !template.ConditionalLogic.Any())
                return new FormConditionalState();

            var dataForConditionalLogic = trigger == "change"
                ? BuildEvaluationData(data, formData, accumulatedForChange)
                : data.Count > 0
                    ? new Dictionary<string, object>(data)
                    : new Dictionary<string, object>(formData);

            var context = new ConditionalLogicContext
            {
                CurrentPageId = pageId,
                CurrentTaskId = taskId,
                IsClientSide = false,
                Trigger = trigger
            };

            var conditionalState = await orchestrator.ApplyConditionalLogicAsync(
                template,
                dataForConditionalLogic,
                context);

            if (conditionalState.FieldValues.Any())
            {
                foreach (var kvp in conditionalState.FieldValues)
                    data[kvp.Key] = kvp.Value;
            }

            return conditionalState;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "CONDITIONAL LOGIC ERROR: {Message}", ex.Message);
            return new FormConditionalState();
        }
    }
}
