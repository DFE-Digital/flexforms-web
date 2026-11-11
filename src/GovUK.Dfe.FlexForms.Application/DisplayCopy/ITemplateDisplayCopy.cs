namespace GovUK.Dfe.FlexForms.Application.DisplayCopy;

/// <summary>
/// Wording for the current template. A template value wins, then the tenant setting, then the FlexForms default wording.
/// </summary>
public interface ITemplateDisplayCopy
{
    string Singular { get; }
    string SingularCapitalised { get; }
    string Plural { get; }
    string PluralCapitalised { get; }

    string DashboardMainHeading { get; }
    string DashboardInProgressHeading { get; }
    string DashboardStartNewHeading { get; }
    string DashboardStartNewHint { get; }
    string DashboardStartNewButtonText { get; }

    string PreviewPageHeading { get; }
    string PreviewSubmitHeading { get; }
    string PreviewSubmitHint { get; }
    string PreviewSubmitButtonText { get; }

    string ContributorsSectionHeading { get; }
    string ContributorsSectionBody { get; }
    string ContributorsInviteButtonText { get; }
    string ContributorsPageHeading { get; }
    string ContributorsIntro { get; }
    string ContributorsSignInText { get; }
    string ContributorsOnceJoinedText { get; }
    string ContributorsAddLaterText { get; }
    string ContributorsAddButtonText { get; }
    string ContributorsProceedButtonText { get; }
    string ContributorsCurrentHeading { get; }

    string ReviewButtonText { get; }
    string ViewButtonText { get; }

    string FileValidationPendingText { get; }
    string FileValidationPassedText { get; }
    string FileValidationFailedText { get; }
}
