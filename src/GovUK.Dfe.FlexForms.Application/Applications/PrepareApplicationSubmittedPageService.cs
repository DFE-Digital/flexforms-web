using System.Text;
using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.Applications;

/// <summary>
/// Resolves per-template confirmation copy for the application-submitted page.
/// </summary>
public interface IPrepareApplicationSubmittedPage
{
    Task ExecuteAsync(ApplicationSubmittedWorkState state, CancellationToken cancellationToken = default);
}

public sealed class PrepareApplicationSubmittedPageService(
    IApplicationsClient applicationsClient,
    IRequestAppConfiguration requestAppConfiguration,
    IApplicationTerminologyProvider terminology,
    ILogger<PrepareApplicationSubmittedPageService> logger) : IPrepareApplicationSubmittedPage
{
    public const string SettingsCategory = "ApplicationSubmittedPage";

    public async Task ExecuteAsync(
        ApplicationSubmittedWorkState state,
        CancellationToken cancellationToken = default)
    {
        var copy = await ResolveCopyAsync(state.ReferenceNumber, cancellationToken);
        state.PanelTitle = FirstNonEmpty(copy.PanelTitle, ApplicationSubmittedPageDefaults.PanelTitle(terminology));
        state.BodyMarkdown = FirstNonEmpty(copy.BodyMarkdown, ApplicationSubmittedPageDefaults.BodyMarkdown(terminology));
    }

    private async Task<ApplicationSubmittedPageCopy> ResolveCopyAsync(
        string referenceNumber,
        CancellationToken cancellationToken)
    {
        var configured = BindConfiguredCopy();
        if (configured.Count == 0)
            return new ApplicationSubmittedPageCopy();

        var keys = await ResolveTemplateKeysAsync(referenceNumber, cancellationToken);
        keys.Add(ApplicationSubmittedPageCopy.DefaultTemplateKey);

        foreach (var key in keys)
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

    private async Task<List<string>> ResolveTemplateKeysAsync(
        string referenceNumber,
        CancellationToken cancellationToken)
    {
        var keys = new List<string>();
        if (string.IsNullOrWhiteSpace(referenceNumber))
            return keys;

        try
        {
            var application = await applicationsClient.GetApplicationByReferenceAsync(
                referenceNumber,
                cancellationToken);

            var templateId = application.TemplateSchema?.TemplateId;
            if (templateId is Guid guid && guid != Guid.Empty)
                keys.Add(guid.ToString());

            foreach (var alias in EmbeddedSchemaTemplateIds(application.TemplateSchema))
            {
                if (!keys.Contains(alias, StringComparer.OrdinalIgnoreCase))
                    keys.Add(alias);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Could not resolve template for submitted page copy using reference {ReferenceNumber}",
                referenceNumber);
        }

        return keys;
    }

    private static IEnumerable<string> EmbeddedSchemaTemplateIds(TemplateSchemaDto? schema)
    {
        if (string.IsNullOrWhiteSpace(schema?.JsonSchema))
            yield break;

        var schemaText = schema.JsonSchema.Trim();
        if (!schemaText.StartsWith('{') && !schemaText.StartsWith('['))
        {
            try
            {
                schemaText = Encoding.UTF8.GetString(Convert.FromBase64String(schemaText));
            }
            catch (FormatException)
            {
                yield break;
            }
        }

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

    private static string FirstNonEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
