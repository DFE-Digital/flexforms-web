using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using Microsoft.Extensions.Configuration;

namespace GovUK.Dfe.FlexForms.Web.Services;

public static class FileValidationUi
{
    public static FileValidationMode ResolveMode(IConfiguration configuration, string? templateId)
    {
        if (templateId is not null
            && Guid.TryParse(templateId, out var id)
            && TryParse(configuration[$"FileValidation:Templates:{id}"], out var templateMode))
        {
            return templateMode;
        }

        return TryParse(configuration["FileValidation:DefaultMode"], out var defaultMode)
            ? defaultMode
            : FileValidationMode.Off;
    }

    public static IReadOnlyList<UploadDto> GetBlockingFiles(
        FileValidationMode mode,
        IEnumerable<UploadDto> files)
    {
        if (mode == FileValidationMode.Off)
            return [];

        return files
            .Where(file => file.ValidationStatus == FileValidationStatus.Failed
                           || (mode == FileValidationMode.RequirePassed
                               && file.ValidationStatus == FileValidationStatus.Pending))
            .ToList();
    }

    public static string StatusText(FileValidationStatus status) => status switch
    {
        FileValidationStatus.Pending => "Validation pending",
        FileValidationStatus.Passed => "Validated",
        FileValidationStatus.Failed => "Validation failed",
        _ => string.Empty
    };

    private static bool TryParse(string? value, out FileValidationMode mode)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && Enum.TryParse(value.Trim(), ignoreCase: true, out mode)
            && Enum.IsDefined(mode))
        {
            return true;
        }

        mode = FileValidationMode.Off;
        return false;
    }
}
