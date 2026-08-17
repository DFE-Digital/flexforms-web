using System.Text;
using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Domain.Templates;
using Microsoft.Extensions.Logging;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Admin;

/// <summary>
/// Loads template versions, validates schema, creates versions, and grants tenant-wide access.
/// </summary>
public interface ITemplateManagerAdmin
{
    Task LoadTemplateDataAsync(TemplateManagerWorkState state, Guid templateId, CancellationToken cancellationToken = default);

    void PrefillNewSchemaIfEmpty(TemplateManagerWorkState state, Guid templateId);

    AdminPageOutcome ValidateNewVersion(TemplateManagerWorkState state);

    Task<AdminPageOutcome> CreateVersionAsync(TemplateManagerWorkState state, Guid templateId, CancellationToken cancellationToken = default);

    string SuggestNextVersion(string? latestVersion, string? currentVersion);

    Task<AdminPageOutcome> GrantToAllUsersAsync(TemplateManagerWorkState state, Guid templateId, CancellationToken cancellationToken = default);
}

public sealed class TemplateManagerAdminService(
    ITemplatesClient templatesClient,
    ITemplateValidationService templateValidationService,
    ILogger<TemplateManagerAdminService> logger) : ITemplateManagerAdmin
{
    public async Task LoadTemplateDataAsync(
        TemplateManagerWorkState state,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger.LogDebug("Loading template data for {TemplateId}", templateId);

            state.SelectedTemplate = state.TenantTemplates.First(template => template.TemplateId == templateId);
            state.SelectedTemplateId = templateId;
            state.LatestVersionNumber = state.SelectedTemplate.LatestVersionNumber;

            var versions = await templatesClient.GetTemplateVersionsAsync(templateId);
            state.AvailableVersions = versions.ToList();

            if (state.AvailableVersions.Count == 0)
            {
                state.CurrentVersionNumber = null;
                state.SelectedVersionNumber = null;
                state.CurrentTemplate = null;
                state.CurrentTemplateJson = null;
                return;
            }

            var requestedVersion = state.SelectedVersionNumber ?? state.SessionVersionNumber;
            var selectedVersion = state.AvailableVersions.FirstOrDefault(v =>
                    !string.IsNullOrWhiteSpace(requestedVersion) &&
                    string.Equals(v.VersionNumber, requestedVersion, StringComparison.OrdinalIgnoreCase))
                ?? state.AvailableVersions[0];

            state.SelectedVersionNumber = selectedVersion.VersionNumber;
            state.CurrentVersionNumber = selectedVersion.VersionNumber;
            state.LatestVersionNumber = state.AvailableVersions[0].VersionNumber;
            state.SessionVersionNumber = selectedVersion.VersionNumber;

            var apiResponse = await templatesClient.GetTemplateSchemaByVersionAsync(
                templateId,
                selectedVersion.VersionNumber);

            var schemaJson = apiResponse.JsonSchema;
            if (string.IsNullOrWhiteSpace(schemaJson))
            {
                state.CurrentTemplate = null;
                state.CurrentTemplateJson = null;
                return;
            }

            var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
            state.CurrentTemplate = JsonSerializer.Deserialize<FormTemplate>(schemaJson, options);
            state.CurrentTemplateJson = state.CurrentTemplate != null
                ? JsonSerializer.Serialize(state.CurrentTemplate, options)
                : PrettyPrintJson(schemaJson);

            logger.LogDebug(
                "Loaded template {TemplateId} version {VersionNumber} with {TaskGroupCount} task groups",
                templateId,
                state.CurrentVersionNumber,
                state.CurrentTemplate?.TaskGroups?.Count ?? 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading template data for {TemplateId}", templateId);
            state.HasError = true;
            state.ErrorMessage = TemplateManagerMessages.LoadFailed;
        }
    }

    public void PrefillNewSchemaIfEmpty(TemplateManagerWorkState state, Guid templateId)
    {
        if (!state.ShowAddVersionForm || !string.IsNullOrWhiteSpace(state.NewSchema))
            return;

        if (!string.IsNullOrWhiteSpace(state.CurrentTemplateJson))
        {
            state.NewSchema = state.CurrentTemplateJson;
        }
        else
        {
            state.NewSchema = StarterFormTemplateSchema.CreateJson(
                templateId.ToString(),
                state.SelectedTemplate?.Name ?? "New template");
            state.NewVersion ??= StarterFormTemplateSchema.DefaultVersionNumber;
        }
    }

    public AdminPageOutcome ValidateNewVersion(TemplateManagerWorkState state)
    {
        var errors = new List<FormValidationError>();

        if (string.IsNullOrWhiteSpace(state.NewVersion))
            errors.Add(new FormValidationError(nameof(TemplateManagerWorkState.NewVersion), TemplateManagerMessages.VersionRequired));

        if (string.IsNullOrWhiteSpace(state.NewSchema))
        {
            errors.Add(new FormValidationError(nameof(TemplateManagerWorkState.NewSchema), TemplateManagerMessages.SchemaRequired));
        }
        else
        {
            var (templateIsValid, validationErrors) = templateValidationService.ValidateTemplateJson(state.NewSchema);
            if (!templateIsValid)
            {
                logger.LogWarning("Template validation failed with {ErrorCount} errors", validationErrors.Count);
                errors.AddRange(validationErrors.Select(error =>
                    new FormValidationError(nameof(TemplateManagerWorkState.NewSchema), error)));
            }
            else
            {
                logger.LogInformation("Template validation passed successfully");
            }
        }

        if (!state.AcknowledgeReportingImpact)
        {
            errors.Add(new FormValidationError(
                nameof(TemplateManagerWorkState.AcknowledgeReportingImpact),
                TemplateManagerMessages.AcknowledgeReportingImpact));
        }

        return errors.Count == 0
            ? AdminPageOutcome.Stay()
            : AdminPageOutcome.Stay(errors: errors);
    }

    public async Task<AdminPageOutcome> CreateVersionAsync(
        TemplateManagerWorkState state,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        var base64Schema = Convert.ToBase64String(Encoding.UTF8.GetBytes(state.NewSchema!));
        await templatesClient.CreateTemplateVersionAsync(
            templateId,
            new CreateTemplateVersionRequest(VersionNumber: state.NewVersion!, JsonSchema: base64Schema));

        logger.LogInformation("Successfully created template version {NewVersion} for {TemplateId}",
            state.NewVersion, templateId);

        return AdminPageOutcome.Redirect(
            routeValues: new Dictionary<string, string?> { ["success"] = "true" });
    }

    public string SuggestNextVersion(string? latestVersion, string? currentVersion) =>
        TemplateVersionPolicy.IncrementPatch(latestVersion ?? currentVersion);

    public async Task<AdminPageOutcome> GrantToAllUsersAsync(
        TemplateManagerWorkState state,
        Guid templateId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await templatesClient.GrantTemplateAccessToAllUsersAsync(templateId, cancellationToken);
            var summary = TemplateManagerMessages.GrantedSummary(
                result.UsersGranted,
                result.UsersAlreadyHadAccess,
                result.TotalUsers);

            logger.LogInformation(
                "Granted template {TemplateId} to all tenant users. Granted={Granted}, AlreadyHad={AlreadyHad}, Total={Total}",
                templateId,
                result.UsersGranted,
                result.UsersAlreadyHadAccess,
                result.TotalUsers);

            state.GrantToAllUsersSummary = summary;
            return AdminPageOutcome.Redirect(successMessage: summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to grant template {TemplateId} to all tenant users", templateId);
            state.HasError = true;
            state.ErrorMessage = TemplateManagerMessages.GrantFailed;
            await LoadTemplateDataAsync(state, templateId, cancellationToken);
            return AdminPageOutcome.Stay(errorMessage: TemplateManagerMessages.GrantFailed);
        }
    }

    private static string PrettyPrintJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
