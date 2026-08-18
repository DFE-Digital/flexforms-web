using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Resolves and persists per-field upload lists (session, collection progress, accumulated data).
/// </summary>
public interface IFormFileFieldService
{
    IReadOnlyList<UploadDto> GetFiles(FormFileFieldContext context, string fieldId);

    void SaveFiles(FormFileFieldContext context, string fieldId, IReadOnlyList<UploadDto> files);

    void ReplaceUploadPlaceholders(Dictionary<string, object> data, FormFileFieldContext context);

    bool ContainsFileName(FormFileFieldContext context, string fieldId, string fileName);
}
