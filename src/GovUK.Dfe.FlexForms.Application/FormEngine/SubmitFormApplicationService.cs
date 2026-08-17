using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Validation;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Submits an application after task-completion and file-validation gates.
/// </summary>
public interface ISubmitFormApplication
{
    Task<FormEngineOutcome> ExecuteAsync(FormEngineWorkState state, CancellationToken cancellationToken = default);
}

public sealed class SubmitFormApplicationService(
    IApplicationStateService applicationStateService,
    IApplicationsClient applicationsClient,
    IFormSessionStore sessionStore,
    IConditionalLogicOrchestrator conditionalLogicOrchestrator,
    ILogger<SubmitFormApplicationService> logger) : ISubmitFormApplication
{
    public async Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        CancellationToken cancellationToken = default)
    {
        if (!state.IsEditable)
        {
            return FormEngineOutcome.Stay(
                formState: FormState.ApplicationPreview,
                errors: [new FormValidationError(string.Empty, FormEngineMessages.NoWritePermission)]);
        }

        if (!applicationStateService.AreAllTasksCompleted(
                state.Template!,
                state.FormData,
                state.ApplicationId,
                state.ApplicationStatus))
        {
            logger.LogWarning("Cannot submit application {ReferenceNumber} - not all tasks completed", state.ReferenceNumber);
            return FormEngineOutcome.Stay(
                formState: FormState.ApplicationPreview,
                errors: [new FormValidationError(string.Empty, FormEngineMessages.AllSectionsMustBeCompleted)]);
        }

        var visibility = new FormEngineVisibilityEvaluator(
            state.Template,
            state.ConditionalState,
            conditionalLogicOrchestrator,
            state.CurrentPageId,
            state.TaskId,
            logger);
        var tasksWithMissingFields = applicationStateService.ValidateAllRequiredFieldsForSubmission(
            state.Template!,
            state.FormData,
            visibility.IsFieldHidden);
        if (tasksWithMissingFields.Count > 0)
        {
            logger.LogWarning(
                "Cannot submit application {ReferenceNumber} - {TaskCount} task(s) have missing required fields: {TaskIds}",
                state.ReferenceNumber,
                tasksWithMissingFields.Count,
                string.Join(", ", tasksWithMissingFields.Keys));

            var taskNames = tasksWithMissingFields.Keys
                .Select(taskId => state.Template?.TaskGroups?
                    .SelectMany(g => g.Tasks)
                    .FirstOrDefault(t => t.TaskId == taskId)?.TaskName ?? taskId)
                .ToList();

            return FormEngineOutcome.Stay(
                formState: FormState.ApplicationPreview,
                errors:
                [
                    new FormValidationError(
                        string.Empty,
                        $"Some sections have missing required information and need to be completed again: {string.Join(", ", taskNames)}")
                ]);
        }

        if (!state.ApplicationId.HasValue)
        {
            logger.LogError("ApplicationId not found during submission for reference {ReferenceNumber}", state.ReferenceNumber);
            return FormEngineOutcome.Stay(
                errors: [new FormValidationError(string.Empty, FormEngineMessages.ApplicationNotFound)]);
        }

        try
        {
            var gate = await applicationsClient.GetFileValidationGateAsync(state.ApplicationId.Value);
            if (gate is { CanSubmit: false })
            {
                var names = string.Join(", ", (gate.BlockingFiles ?? []).Select(f => f.OriginalFileName));
                return FormEngineOutcome.Stay(
                    formState: FormState.ApplicationPreview,
                    errors:
                    [
                        new FormValidationError(
                            string.Empty,
                            $"Some uploaded files failed validation or are still being checked: {names}")
                    ],
                    fileValidationBlocksSubmit: true,
                    blockingFiles: gate.BlockingFiles ?? []);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not evaluate file validation gate for application {ApplicationId}", state.ApplicationId);
        }

        try
        {
            logger.LogInformation(
                "Attempting to submit application {ApplicationId} with reference {ReferenceNumber}",
                state.ApplicationId.Value,
                state.ReferenceNumber);

            var submittedApplication = await applicationsClient.SubmitApplicationAsync(state.ApplicationId.Value);
            if (submittedApplication != null)
            {
                sessionStore.SetString(
                    $"ApplicationStatus_{state.ApplicationId.Value}",
                    submittedApplication.Status?.ToString() ?? "Submitted");
                logger.LogInformation(
                    "Successfully submitted application {ApplicationId} with reference {ReferenceNumber}",
                    state.ApplicationId.Value,
                    state.ReferenceNumber);
            }
            else
            {
                logger.LogWarning("Submit API returned null for application {ApplicationId}", state.ApplicationId.Value);
            }

            return FormEngineOutcome.RedirectToPage(
                "/Applications/ApplicationSubmitted",
                new { referenceNumber = state.ReferenceNumber });
        }
        catch (Exception ex) when (ex is not ExternalApplicationsException)
        {
            logger.LogError(
                ex,
                "Failed to submit application {ApplicationId} with reference {ReferenceNumber}",
                state.ApplicationId.Value,
                state.ReferenceNumber);

            return FormEngineOutcome.Stay(
                formState: FormState.ApplicationPreview,
                errors:
                [
                    new FormValidationError(
                        string.Empty,
                        $"An error occurred while submitting your application: {ex.Message}. Please try again.")
                ]);
        }
    }
}
