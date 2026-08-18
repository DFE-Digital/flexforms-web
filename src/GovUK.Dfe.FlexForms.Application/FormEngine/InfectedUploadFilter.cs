using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

public sealed class InfectedUploadFilter(
    IInfectedFileStore infectedFileStore,
    ILogger<InfectedUploadFilter> logger) : IInfectedUploadFilter
{
    public List<UploadDto> FilterList(IReadOnlyList<UploadDto>? files, string? applicationId)
    {
        if (files == null || files.Count == 0)
            return files?.ToList() ?? [];

        try
        {
            var infectedFileIds = new HashSet<Guid>();
            foreach (var file in files)
            {
                var fileIdExists = infectedFileStore.IsFileInfected(file.Id);
                var filenameExists = !string.IsNullOrEmpty(applicationId)
                    && !string.IsNullOrEmpty(file.OriginalFileName)
                    && infectedFileStore.IsFileNameInfected(applicationId, file.OriginalFileName);

                if (fileIdExists || filenameExists)
                    infectedFileIds.Add(file.Id);
            }

            if (infectedFileIds.Count == 0)
                return files.ToList();

            logger.LogWarning(
                "Filtered out {RemovedCount} infected file(s) from a list of {FileCount}",
                infectedFileIds.Count,
                files.Count);

            return files.Where(f => !infectedFileIds.Contains(f.Id)).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to filter infected files; returning original list of {FileCount}", files.Count);
            return files.ToList();
        }
    }

    public string FilterUploadDataJson(string? uploadDataJson, string? applicationId)
    {
        if (string.IsNullOrWhiteSpace(uploadDataJson))
            return uploadDataJson ?? string.Empty;

        try
        {
            var files = JsonSerializer.Deserialize<List<UploadDto>>(uploadDataJson);
            if (files == null)
                return uploadDataJson;

            return JsonSerializer.Serialize(FilterList(files, applicationId));
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "Failed to parse upload data as file list, returning original value");
            return uploadDataJson;
        }
    }
}
