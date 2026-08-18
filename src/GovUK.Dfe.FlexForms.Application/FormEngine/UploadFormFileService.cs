using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Validation;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

public sealed record UploadFormFileRequest(
    Guid ApplicationId,
    string FieldId,
    string? ReturnUrl,
    string? UploadDescription,
    Stream Content,
    string FileName,
    string? ContentType,
    string PageErrorContextKey,
    bool HasFile);

/// <summary>
/// Uploads a file into session-backed field storage without persisting it to the application response.
/// </summary>
public interface IUploadFormFile
{
    Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        UploadFormFileRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class UploadFormFileService(
    IFormFileFieldService formFileFieldService,
    IFileUploadService fileUploadService,
    IInfectedUploadFilter infectedUploadFilter,
    ILogger<UploadFormFileService> logger) : IUploadFormFile
{
    public async Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        UploadFormFileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (state.Template == null)
            state.Template = FormEngineConstants.CreateDummyTemplate();

        var context = new FormFileFieldContext(request.ApplicationId, state.FlowId, state.InstanceId);

        if (!request.HasFile)
            return FileRejected(request, context, FormEngineMessages.SelectAFile);

        if (formFileFieldService.ContainsFileName(context, request.FieldId, request.FileName))
            return FileRejected(request, context, FormEngineMessages.DuplicateFileName);

        var fileParam = new FileParameter(request.Content, request.FileName, request.ContentType);
        var uploadedFile = await fileUploadService.UploadFileAsync(
            request.ApplicationId,
            request.FileName,
            request.UploadDescription,
            fileParam,
            cancellationToken);

        var currentFieldFiles = formFileFieldService.GetFiles(context, request.FieldId).ToList();
        if (currentFieldFiles.All(cf => cf.Id != uploadedFile.Id))
        {
            logger.LogInformation(
                "Adding newly uploaded file {FileId} ({FileName}) to field {FieldId}",
                uploadedFile.Id,
                uploadedFile.OriginalFileName,
                request.FieldId);
            currentFieldFiles.Add(uploadedFile);
        }

        currentFieldFiles = infectedUploadFilter.FilterList(currentFieldFiles, request.ApplicationId.ToString());
        formFileFieldService.SaveFiles(context, request.FieldId, currentFieldFiles);

        var successMessage = $"Your file '{request.FileName}' uploaded.";
        logger.LogInformation(
            "Cleared FormErrorStore (fieldKey: {FieldId}, contextKey: {PageContext}) and ModelState after successful upload",
            request.FieldId,
            request.PageErrorContextKey);

        string[] keysToClear = [request.FieldId, request.PageErrorContextKey];
        string[] modelStateKeys = [request.FieldId, $"Data[{request.FieldId}]"];

        if (!string.IsNullOrEmpty(request.ReturnUrl))
        {
            return FormEngineOutcome.Redirect(
                request.ReturnUrl,
                successMessage: successMessage,
                errorStoreKeysToClear: keysToClear,
                modelStateKeysToRemove: modelStateKeys,
                notificationContext: $"file-upload|{uploadedFile.Id}");
        }

        return FormEngineOutcome.Stay(
            successMessage: successMessage,
            files: currentFieldFiles,
            errorStoreKeysToClear: keysToClear,
            modelStateKeysToRemove: modelStateKeys,
            notificationContext: $"file-upload|{uploadedFile.Id}");
    }

    private FormEngineOutcome FileRejected(
        UploadFormFileRequest request,
        FormFileFieldContext context,
        string message)
    {
        IReadOnlyList<UploadDto> files = formFileFieldService.GetFiles(context, request.FieldId);
        var errors = new[] { new FormValidationError("UploadFile", message) };
        var persistKey = string.IsNullOrEmpty(request.FieldId) ? null : request.FieldId;

        if (!string.IsNullOrEmpty(request.ReturnUrl))
        {
            return FormEngineOutcome.Redirect(
                request.ReturnUrl,
                errors: errors,
                persistErrors: persistKey != null,
                errorContextKey: persistKey,
                files: files,
                errorMessage: message);
        }

        return FormEngineOutcome.Stay(
            errors: errors,
            persistErrors: persistKey != null,
            errorContextKey: persistKey,
            errorMessage: message,
            files: files);
    }
}
