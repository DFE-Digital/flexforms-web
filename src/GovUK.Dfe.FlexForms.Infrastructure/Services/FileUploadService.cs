using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    public class FileUploadService(IApplicationsClient applicationsClient, ILogger<FileUploadService> logger)
        : IFileUploadService
    {
        public async Task<UploadDto> UploadFileAsync(Guid applicationId, string? name = null, string? description = null, FileParameter file = null!, CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInformation("Uploading file for application {ApplicationId}", applicationId);
                var upload = await applicationsClient.UploadFileAsync(applicationId, name, description, file, cancellationToken);
                logger.LogInformation("File uploaded successfully for application {ApplicationId}", applicationId);
                return upload;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error uploading file for application {ApplicationId}", applicationId);
                throw;
            }
        }

        public async Task<IReadOnlyList<UploadDto>> GetFilesForApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Getting files for application {ApplicationId}", applicationId);
            return await applicationsClient.GetFilesForApplicationAsync(applicationId, cancellationToken);
        }

        public async Task<FileResponse> DownloadFileAsync(Guid fileId, Guid applicationId, CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInformation("Downloading file {FileId} for application {ApplicationId}", fileId, applicationId);
                return await applicationsClient.DownloadFileAsync(fileId, applicationId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error downloading file {FileId} for application {ApplicationId}", fileId, applicationId);
                throw;
            }
        }

        public async Task DeleteFileAsync(Guid fileId, Guid applicationId, CancellationToken cancellationToken = default)
        {
            try
            {
                logger.LogInformation("Deleting file {FileId} for application {ApplicationId}", fileId, applicationId);
                await applicationsClient.DeleteFileAsync(fileId, applicationId, cancellationToken);
                logger.LogInformation("File {FileId} deleted for application {ApplicationId}", fileId, applicationId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error deleting file {FileId} for application {ApplicationId}", fileId, applicationId);
                throw;
            }
        }
    }
} 