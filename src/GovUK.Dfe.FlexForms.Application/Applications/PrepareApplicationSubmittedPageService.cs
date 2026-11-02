using System.Text;
using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.DisplayCopy;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Applications;

/// <summary>
/// Resolves confirmation copy for the application-submitted page.
/// Template <c>displayCopy.submittedPage</c> wins, then existing tenant settings (including
/// per-template keys still stored there), then the built-in sentence.
/// </summary>
public interface IPrepareApplicationSubmittedPage
{
    Task ExecuteAsync(ApplicationSubmittedWorkState state, CancellationToken cancellationToken = default);
}

public sealed class PrepareApplicationSubmittedPageService(
    IApplicationsClient applicationsClient,
    IRequestAppConfiguration requestAppConfiguration,
    IApplicationTerminologyProvider terminology,
    ITemplateCopyContext templateCopyContext,
    ILogger<PrepareApplicationSubmittedPageService> logger) : IPrepareApplicationSubmittedPage
{
    public const string SettingsCategory = "ApplicationSubmittedPage";

    public async Task ExecuteAsync(
        ApplicationSubmittedWorkState state,
        CancellationToken cancellationToken = default)
    {
        var application = await TryGetApplicationAsync(state.ReferenceNumber, cancellationToken);
        BindTemplateCopy(application);

        var templateCopy = templateCopyContext.Template?.DisplayCopy?.SubmittedPage;
        var tenantCopy = ResolveTenantCopy(application);

        state.PanelTitle = ResolveField(
            templateCopy?.PanelTitle,
            tenantCopy.PanelTitle,
            ApplicationSubmittedPageDefaults.PanelTitle(terminology));
        state.BodyMarkdown = ResolveField(
            templateCopy?.BodyMarkdown,
            tenantCopy.BodyMarkdown,
            ApplicationSubmittedPageDefaults.BodyMarkdown(terminology));
    }

    private ApplicationSubmittedPageCopy ResolveTenantCopy(ApplicationDto? application)
    {
        var configured = BindConfiguredCopy();
        if (configured.Count == 0)
            return new ApplicationSubmittedPageCopy();

        foreach (var key in ResolveTemplateKeys(application))
        {
            if (configured.TryGetValue(key, out var copy))
                return copy;
        }

        return new ApplicationSubmittedPageCopy();
    }

    private Dictionary<string, ApplicationSubmittedPageCopy> BindConfiguredCopy()
    {
        var result = new Dictionary<string, ApplicationSubmittedPageCopy>(StringComparer.OrdinalIgnoreCase);
        var section = requestAppConfiguration.GetSection(SettingsCategory);

        foreach (var child in section.GetChildren())
        {
            if (string.IsNullOrWhiteSpace(child.Key))
                continue;

            result[child.Key] = new ApplicationSubmittedPageCopy
            {
                PanelTitle = child["PanelTitle"],
                BodyMarkdown = child["BodyMarkdown"]
            };
        }

        return result;
    }

    private async Task<ApplicationDto?> TryGetApplicationAsync(
        string referenceNumber,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(referenceNumber))
            return null;

        try
        {
            return await applicationsClient.GetApplicationByReferenceAsync(
                referenceNumber,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not resolve template for submitted page copy using reference {ReferenceNumber}",
                referenceNumber);
            return null;
        }
    }

    private void BindTemplateCopy(ApplicationDto? application)
    {
        var schemaText = DecodeSchemaJson(application?.TemplateSchema?.JsonSchema);
        if (schemaText is null)
            return;

        templateCopyContext.TryUseJson(schemaText);
    }

    private List<string> ResolveTemplateKeys(ApplicationDto? application)
    {
        var keys = new List<string>();
        var templateId = application?.TemplateSchema?.TemplateId;
        if (templateId is Guid guid && guid != Guid.Empty)
            keys.Add(guid.ToString());

        var schemaAlias = templateCopyContext.Template?.TemplateId;
        if (!string.IsNullOrWhiteSpace(schemaAlias)
            && !keys.Contains(schemaAlias, StringComparer.OrdinalIgnoreCase))
        {
            keys.Add(schemaAlias);
        }

        foreach (var alias in EmbeddedSchemaTemplateIds(application?.TemplateSchema))
        {
            if (!keys.Contains(alias, StringComparer.OrdinalIgnoreCase))
                keys.Add(alias);
        }

        keys.Add(ApplicationSubmittedPageCopy.DefaultTemplateKey);
        return keys;
    }

    private static IEnumerable<string> EmbeddedSchemaTemplateIds(TemplateSchemaDto? schema)
    {
        var schemaText = DecodeSchemaJson(schema?.JsonSchema);
        if (schemaText is null)
            yield break;

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(schemaText);
        }
        catch (JsonException)
        {
            yield break;
        }

        using (doc)
        {
            if (doc.RootElement.TryGetProperty("templateId", out var embeddedId)
                && embeddedId.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(embeddedId.GetString()))
            {
                yield return embeddedId.GetString()!.Trim();
            }
        }
    }

    private static string? DecodeSchemaJson(string? jsonSchema)
    {
        if (string.IsNullOrWhiteSpace(jsonSchema))
            return null;

        var schemaText = jsonSchema.Trim();
        if (!schemaText.StartsWith('{') && !schemaText.StartsWith('['))
        {
            try
            {
                schemaText = Encoding.UTF8.GetString(Convert.FromBase64String(schemaText));
            }
            catch (FormatException)
            {
                return null;
            }
        }

        return schemaText;
    }

    private string ResolveField(string? templateValue, string? tenantValue, string codeDefault) =>
        DisplayCopyPlaceholders.Apply(
            FirstNonEmpty(templateValue, FirstNonEmpty(tenantValue, codeDefault)),
            templateCopyContext.Template?.TemplateName,
            terminology.Singular,
            terminology.Plural,
            requestAppConfiguration["Layout:ContributorEmail"]);

    private static string FirstNonEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
