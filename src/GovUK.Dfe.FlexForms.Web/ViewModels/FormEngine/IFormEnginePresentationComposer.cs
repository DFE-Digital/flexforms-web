using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;

/// <summary>
/// Builds Razor-ready view models for application preview and collection-flow summaries.
/// </summary>
public interface IFormEnginePresentationComposer
{
    /// <summary>
    /// Builds the check-your-answers preview from the template and current form data.
    /// </summary>
    ApplicationPreviewViewModel BuildPreview(FormEnginePresentationContext context);

    /// <summary>
    /// Builds collection-flow sections for a multi-collection task summary.
    /// </summary>
    IReadOnlyList<CollectionFlowSectionViewModel> BuildCollectionFlows(
        FormEnginePresentationContext context,
        TaskModel task);
}
