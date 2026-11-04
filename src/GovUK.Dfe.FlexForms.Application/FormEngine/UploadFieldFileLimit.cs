using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Maximum number of files an upload question accepts: <see cref="ComplexFieldConfiguration.MaxSelections"/>
/// on the tenant's complex field (FormEngine:ComplexFields). 0 means no limit.
/// </summary>
public static class UploadFieldFileLimit
{
    public static int Resolve(
        FormTemplate? template,
        string fieldId,
        IComplexFieldConfigurationService complexFieldConfigurationService)
    {
        var complexFieldId = FindField(template, fieldId)?.ComplexField?.Id;
        if (string.IsNullOrWhiteSpace(complexFieldId))
            return 0;

        return Math.Max(0, complexFieldConfigurationService.GetConfiguration(complexFieldId)?.MaxSelections ?? 0);
    }

    /// <summary>
    /// Finds a field on any task page, collection flow page or derived flow page.
    /// </summary>
    public static Field? FindField(FormTemplate? template, string fieldId)
    {
        if (template?.TaskGroups is null || string.IsNullOrWhiteSpace(fieldId))
            return null;

        return template.TaskGroups
            .SelectMany(group => group.Tasks ?? [])
            .SelectMany(PagesOf)
            .SelectMany(page => page.Fields ?? [])
            .FirstOrDefault(field => string.Equals(field.FieldId, fieldId, StringComparison.Ordinal));
    }

    private static IEnumerable<Page> PagesOf(Domain.Models.Task task) =>
        (task.Pages ?? [])
            .Concat(task.Summary?.Flows?.SelectMany(flow => flow.Pages ?? []) ?? [])
            .Concat(task.Summary?.DerivedFlows?.SelectMany(flow => flow.Pages ?? []) ?? []);
}
