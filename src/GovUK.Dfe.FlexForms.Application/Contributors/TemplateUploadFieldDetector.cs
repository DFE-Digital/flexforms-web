using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Contributors;

/// <summary>
/// Detects whether a form template includes any file-upload questions.
/// </summary>
public static class TemplateUploadFieldDetector
{
    public static bool HasUploadFields(
        FormTemplate template,
        IComplexFieldConfigurationService complexFieldConfigurationService)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(complexFieldConfigurationService);

        foreach (var field in EnumerateFields(template))
        {
            if (string.Equals(field.Type, "upload", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.Equals(field.Type, "complexField", StringComparison.OrdinalIgnoreCase)
                || field.ComplexField is null
                || string.IsNullOrWhiteSpace(field.ComplexField.Id))
            {
                continue;
            }

            var configuration = complexFieldConfigurationService.GetConfiguration(field.ComplexField.Id);
            if (string.Equals(configuration.FieldType, "upload", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IEnumerable<Field> EnumerateFields(FormTemplate template)
    {
        foreach (var group in template.TaskGroups ?? [])
        {
            foreach (var task in group.Tasks ?? [])
            {
                foreach (var field in EnumerateFieldsOnPages(task.Pages))
                    yield return field;

                // Collection / derived flows keep their pages on Summary, not Task.Pages.
                foreach (var flow in task.Summary?.Flows ?? [])
                {
                    foreach (var field in EnumerateFieldsOnPages(flow.Pages))
                        yield return field;
                }

                foreach (var flow in task.Summary?.DerivedFlows ?? [])
                {
                    foreach (var field in EnumerateFieldsOnPages(flow.Pages))
                        yield return field;
                }
            }
        }
    }

    private static IEnumerable<Field> EnumerateFieldsOnPages(IEnumerable<Page>? pages)
    {
        foreach (var page in pages ?? [])
        {
            foreach (var field in page.Fields ?? [])
                yield return field;
        }
    }
}
