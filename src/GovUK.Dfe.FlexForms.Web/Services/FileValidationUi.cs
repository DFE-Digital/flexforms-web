using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Domain.Models;
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

    public const string DefaultPendingText = "Validation pending";
    public const string DefaultPassedText = "Validated";
    public const string DefaultFailedText = "Validation failed";

    public static string StatusText(FileValidationStatus status) => StatusText(status, null);

    public static string StatusText(FileValidationStatus status, FileValidationStatusText? overrides) => status switch
    {
        FileValidationStatus.Pending => Pick(overrides?.Pending, DefaultPendingText),
        FileValidationStatus.Passed => Pick(overrides?.Passed, DefaultPassedText),
        FileValidationStatus.Failed => Pick(overrides?.Failed, DefaultFailedText),
        _ => string.Empty
    };

    private static string Pick(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

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
