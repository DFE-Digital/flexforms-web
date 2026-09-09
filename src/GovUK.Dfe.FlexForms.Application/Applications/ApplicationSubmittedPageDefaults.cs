using GovUK.Dfe.FlexForms.Application.Interfaces;

namespace GovUK.Dfe.FlexForms.Application.Applications;

/// <summary>
/// Platform fallback copy for the application-submitted page when a tenant has not configured a template.
/// </summary>
public static class ApplicationSubmittedPageDefaults
{
    public static string PanelTitle(IApplicationTerminologyProvider terminology) =>
        $"{terminology.SingularCapitalised} submitted";

    public static string BodyMarkdown(IApplicationTerminologyProvider terminology)
    {
        var singular = terminology.Singular;
        return
            $"""
            We've sent you a confirmation email with your reference number.

            ## What happens next

            Your {singular} will be assigned to a staff member in DfE's Regions Group.

            The staff member will contact you:

            - if we need anything else
            - when the {singular} is ready for decision

            You must have approval for your {singular} from DfE's Regions Group before you:

            - make any changes to the articles of association or any other trust documents
            - engage with stakeholders of the trust that academies are leaving

            ## Contact us

            If you have any questions about your {singular}, you can email [RegionalServices.RG@education.gov.uk](mailto:RegionalServices.RG@education.gov.uk).
            """;
    }
}
