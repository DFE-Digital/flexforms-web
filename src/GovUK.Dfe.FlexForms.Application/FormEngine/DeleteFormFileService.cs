using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

public sealed record DeleteFormFileRequest(
    Guid ApplicationId,
    Guid FileId,
    string FieldId,
    string? ReturnUrl,
    bool Confirmed);

/// <summary>
/// Deletes an uploaded file and persists the remaining field list.
/// </summary>
public interface IDeleteFormFile
{
    Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        DeleteFormFileRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class DeleteFormFileService(
    IFormFileFieldService formFileFieldService,
    IFileUploadService fileUploadService,
    IApplicationResponseService applicationResponseService,
    ILogger<DeleteFormFileService> logger) : IDeleteFormFile
{
    public async Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        DeleteFormFileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (state.Template == null)
            state.Template = FormEngineConstants.CreateDummyTemplate();

        if (!request.Confirmed)
        {
            logger.LogInformation("DeleteFile handler executing for validation - file will not be deleted yet");
            return FormEngineOutcome.Redirect(request.ReturnUrl ?? $"/applications/{state.ReferenceNumber}");
        }

        try
        {
            await fileUploadService.DeleteFileAsync(request.FileId, request.ApplicationId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete file {FileId} for application {ApplicationId}", request.FileId, request.ApplicationId);
            throw;
        }

        var context = new FormFileFieldContext(request.ApplicationId, state.FlowId, state.InstanceId);
        var currentFieldFiles = formFileFieldService.GetFiles(context, request.FieldId).ToList();
        currentFieldFiles.RemoveAll(f => f.Id == request.FileId);
        formFileFieldService.SaveFiles(context, request.FieldId, currentFieldFiles);
        await SaveUploadedFilesToResponseAsync(request.ApplicationId, request.FieldId, currentFieldFiles, cancellationToken);

        if (!string.IsNullOrEmpty(request.ReturnUrl))
        {
            return FormEngineOutcome.Redirect(
                request.ReturnUrl,
                successMessage: FormEngineMessages.FileDeleted,
                notificationContext: $"file-delete|{request.FileId}");
        }

        return FormEngineOutcome.Stay(
            successMessage: FormEngineMessages.FileDeleted,
            files: currentFieldFiles,
            notificationContext: $"file-delete|{request.FileId}");
    }

    private async Task SaveUploadedFilesToResponseAsync(
        Guid appId,
        string fieldId,
        IReadOnlyList<UploadDto> files,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(fieldId))
            return;

        var json = JsonSerializer.Serialize(files);
        await applicationResponseService.SaveApplicationResponseAsync(
            appId,
            new Dictionary<string, object> { { fieldId, json } },
            cancellationToken);
    }
}
