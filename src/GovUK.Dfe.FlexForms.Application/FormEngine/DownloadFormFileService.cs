using System.Net;
using System.Text.RegularExpressions;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

public sealed record DownloadFormFileRequest(Guid ApplicationId, Guid FileId);

/// <summary>
/// Downloads an uploaded file as a stream with content-type and file name.
/// </summary>
public interface IDownloadFormFile
{
    Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        DownloadFormFileRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class DownloadFormFileService(
    IFileUploadService fileUploadService,
    ILogger<DownloadFormFileService> logger) : IDownloadFormFile
{
    public async Task<FormEngineOutcome> ExecuteAsync(
        FormEngineWorkState state,
        DownloadFormFileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (state.Template == null)
            state.Template = FormEngineConstants.CreateDummyTemplate();

        var fileResponse = await fileUploadService.DownloadFileAsync(
            request.FileId,
            request.ApplicationId,
            cancellationToken);

        var contentType = fileResponse.Headers.TryGetValue("Content-Type", out var ct)
            ? ct.FirstOrDefault()
            : "application/octet-stream";

        var fileName = "downloadedfile";
        if (fileResponse.Headers.TryGetValue("Content-Disposition", out var cd))
        {
            var disposition = cd.FirstOrDefault();
            if (!string.IsNullOrEmpty(disposition))
            {
                var fileNameMatch = Regex.Match(
                    disposition,
                    @"filename\*=UTF-8''(?<fileName>.+)|filename=""?(?<fileName>[^\"";]+)""?");
                if (fileNameMatch.Success)
                    fileName = WebUtility.UrlDecode(fileNameMatch.Groups["fileName"].Value);
            }
        }

        logger.LogInformation(
            "Downloading file {FileId} for application {ApplicationId}",
            request.FileId,
            request.ApplicationId);

        return FormEngineOutcome.File(fileResponse.Stream, contentType ?? "application/octet-stream", fileName);
    }
}
