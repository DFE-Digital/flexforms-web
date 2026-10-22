using GovUK.Dfe.FlexForms.Application.Dashboard;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

public sealed record ApplicationMetadataRow(string Label, string DisplayValue);

public sealed record ApplicationMetadataResolveContext(
    string ReferenceNumber,
    string? FormVersion,
    string? LeadApplicantName,
    string? LeadApplicantEmail,
    string LeadApplicantLabel,
    string ApplicationSingularCapitalised,
    IReadOnlyDictionary<string, object>? FormData);

public interface IApplicationMetadataResolver
{
    IReadOnlyList<ApplicationMetadataRow> Resolve(FormTemplate template, ApplicationMetadataResolveContext context);
}

/// <summary>
/// Resolves template <c>applicationMetaData</c> binding paths for the task list header.
/// </summary>
public sealed class ApplicationMetadataResolver : IApplicationMetadataResolver
{
    public IReadOnlyList<ApplicationMetadataRow> Resolve(
        FormTemplate template,
        ApplicationMetadataResolveContext context)
    {
        if (template.ApplicationMetaData is null)
            return BuildDefaultRows(context);

        var configured = template.ApplicationMetaData.Items;
        if (configured is null || configured.Count == 0)
            return BuildDefaultRows(context);

        var formData = ToFormDataDictionary(context.FormData);
        var rows = new List<ApplicationMetadataRow>();

        foreach (var item in configured.Take(ApplicationMetadataConfiguration.MaxItems))
        {
            var label = item.Label?.Trim() ?? string.Empty;
            var binding = item.FieldId?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(label) || string.IsNullOrEmpty(binding))
                continue;

            var display = ResolveBinding(binding, context, formData);
            rows.Add(new ApplicationMetadataRow(label, display));
        }

        return rows;
    }

    private static IReadOnlyList<ApplicationMetadataRow> BuildDefaultRows(ApplicationMetadataResolveContext context) =>
    [
        new($"{context.ApplicationSingularCapitalised} reference", context.ReferenceNumber),
        new($"{context.ApplicationSingularCapitalised} version", context.FormVersion ?? string.Empty),
        new(context.LeadApplicantLabel, context.LeadApplicantName ?? string.Empty)
    ];

    private static string ResolveBinding(
        string binding,
        ApplicationMetadataResolveContext context,
        IReadOnlyDictionary<string, object> formData)
    {
        var path = binding.Trim();
        if (path.Length == 0)
            return string.Empty;

        if (path.StartsWith("field.", StringComparison.OrdinalIgnoreCase))
            return DashboardAnswerReader.GetDisplayValue(path["field.".Length..], formData);

        if (!path.StartsWith("application.", StringComparison.OrdinalIgnoreCase))
            return DashboardAnswerReader.GetDisplayValue(path, formData);

        var key = path["application.".Length..];
        return key switch
        {
            _ when key.Equals("reference", StringComparison.OrdinalIgnoreCase) => context.ReferenceNumber,
            _ when key.Equals("version", StringComparison.OrdinalIgnoreCase) => context.FormVersion ?? string.Empty,
            _ when key.Equals("leadApplicant", StringComparison.OrdinalIgnoreCase)
                || key.Equals("leadApplicantName", StringComparison.OrdinalIgnoreCase) => context.LeadApplicantName ?? string.Empty,
            _ when key.Equals("leadApplicantEmail", StringComparison.OrdinalIgnoreCase) => context.LeadApplicantEmail ?? string.Empty,
            _ when key.StartsWith("field.", StringComparison.OrdinalIgnoreCase) =>
                DashboardAnswerReader.GetDisplayValue(key["field.".Length..], formData),
            _ => string.Empty
        };
    }

    private static IReadOnlyDictionary<string, object> ToFormDataDictionary(
        IReadOnlyDictionary<string, object>? formData)
    {
        if (formData is null || formData.Count == 0)
            return new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in formData)
        {
            if (!string.IsNullOrWhiteSpace(key) && value is not null)
                map[key] = value;
        }

        return map;
    }
}
