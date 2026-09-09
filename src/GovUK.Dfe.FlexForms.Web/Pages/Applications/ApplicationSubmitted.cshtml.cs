using GovUK.Dfe.FlexForms.Application.Applications;
using GovUK.Dfe.FlexForms.Web.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Applications;

public sealed class ApplicationSubmittedModel(
    IPrepareApplicationSubmittedPage prepareApplicationSubmittedPage) : PageModel
{
    [BindProperty(SupportsGet = true, Name = "referenceNumber")]
    public string ReferenceNumber { get; set; } = string.Empty;

    public string PanelTitle { get; private set; } = string.Empty;

    public string BodyHtml { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var state = new ApplicationSubmittedWorkState
        {
            ReferenceNumber = ReferenceNumber ?? string.Empty
        };

        await prepareApplicationSubmittedPage.ExecuteAsync(state, cancellationToken);

        PanelTitle = state.PanelTitle;
        BodyHtml = MarkdownSafe.ToSafeGovUkHtml(state.BodyMarkdown);
    }
}
