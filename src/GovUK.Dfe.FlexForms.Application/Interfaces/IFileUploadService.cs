using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;

namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    /// <summary>
    /// Service for managing file uploads for applications
    /// </summary>
    public interface IFileUploadService
    {
        Task<UploadDto> UploadFileAsync(Guid applicationId, string? name = null, string? description = null, FileParameter file = null!, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<UploadDto>> GetFilesForApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default);
        Task<FileResponse> DownloadFileAsync(Guid fileId, Guid applicationId, CancellationToken cancellationToken = default);
        Task DeleteFileAsync(Guid fileId, Guid applicationId, CancellationToken cancellationToken = default);
    }
} 