namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

public sealed class SummaryFileLinkViewModel
{
    public required Guid FileId { get; init; }
    public required string FileName { get; init; }
    public required string ReferenceNumber { get; init; }
    public required string TaskId { get; init; }
    public Guid? ApplicationId { get; init; }
    public string? PageId { get; init; }
}
