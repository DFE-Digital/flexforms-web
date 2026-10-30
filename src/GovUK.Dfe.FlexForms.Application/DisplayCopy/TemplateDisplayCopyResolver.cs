using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Application.DisplayCopy;

public sealed class TemplateDisplayCopyResolver(
    ITemplateCopyContext templateCopy,
    IOptions<ApplicationTerminologyOptions> terminology,
    IOptions<DashboardOptions> dashboard,
    IOptions<ApplicationPreviewOptions> preview,
    IOptions<ContributorsOptions> contributors,
    IOptions<TaskListOptions> taskList,
    IRequestAppConfiguration configuration) : ITemplateDisplayCopy
{
    public string Singular =>
        First(Copy?.Terminology?.Singular, terminology.Value.Singular);

    public string SingularCapitalised => Capitalise(Singular);

    public string Plural =>
        First(Copy?.Terminology?.Plural, terminology.Value.Plural);

    public string PluralCapitalised => Capitalise(Plural);

    public string DashboardMainHeading =>
        Text(Copy?.Dashboard?.MainHeading, dashboard.Value.MainHeading, $"Your {Plural}");

    public string DashboardInProgressHeading =>
        Text(Copy?.Dashboard?.InProgressHeading, dashboard.Value.InProgressHeading, $"{PluralCapitalised} in progress");

    public string DashboardStartNewHeading =>
        Text(Copy?.Dashboard?.StartNewHeading, dashboard.Value.StartNewHeading, $"Start a new {Singular}");

    public string DashboardStartNewHint =>
        Text(
            Copy?.Dashboard?.StartNewHint,
            dashboard.Value.StartNewHint,
            $"If you start an {Singular}, you will be the lead applicant for it.");

    public string DashboardStartNewButtonText =>
        Text(Copy?.Dashboard?.StartNewButtonText, dashboard.Value.StartNewButtonText, $"Start new {Singular}");

    public string PreviewPageHeading =>
        Text(Copy?.Preview?.PageHeading, preview.Value.PageHeading, "Check your answers");

    public string PreviewSubmitHeading =>
        Text(Copy?.Preview?.SubmitHeading, preview.Value.SubmitHeading, $"Submit your {Singular}");

    public string PreviewSubmitHint =>
        Text(
            Copy?.Preview?.SubmitHint,
            preview.Value.SubmitHint,
            $"By submitting this {Singular} you are confirming that, to the best of your knowledge, the details you are providing are correct.");

    public string PreviewSubmitButtonText =>
        Text(Copy?.Preview?.SubmitButtonText, preview.Value.SubmitButtonText, "Submit");

    public string ContributorsSectionHeading =>
        Text(Copy?.Contributors?.SectionHeading, contributors.Value.SectionHeading, "Contributors");

    public string ContributorsSectionBody =>
        Text(
            Copy?.Contributors?.SectionBody,
            contributors.Value.SectionBody,
            $"You can invite other people to complete or view parts of this {Singular}.");

    public string ContributorsInviteButtonText =>
        Text(Copy?.Contributors?.InviteButtonText, contributors.Value.InviteButtonText, "Invite contributors");

    public string ContributorsPageHeading =>
        Text(Copy?.Contributors?.PageHeading, contributors.Value.PageHeading, "Invite contributors");

    public string ContributorsIntro =>
        Text(
            Copy?.Contributors?.Intro,
            contributors.Value.Intro,
            $"Invite people to help you complete your {Singular} by providing their email address and name.");

    public string ContributorsSignInText =>
        Text(
            Copy?.Contributors?.SignInText,
            contributors.Value.SignInText,
            "They must have a DfE Sign-in account. They can only create an account using a business email address with their name. For example, {contributor_email}.");

    public string ContributorsOnceJoinedText =>
        Text(
            Copy?.Contributors?.OnceJoinedText,
            contributors.Value.OnceJoinedText,
            $"Once a contributor joins, they will be able to view and edit the {Singular} and see each contributor's email address.");

    public string ContributorsAddLaterText =>
        Text(Copy?.Contributors?.AddLaterText, contributors.Value.AddLaterText, "You can add contributors now or later.");

    public string ContributorsAddButtonText =>
        Text(Copy?.Contributors?.AddButtonText, contributors.Value.AddButtonText, "Add a contributor");

    public string ContributorsProceedButtonText =>
        Text(Copy?.Contributors?.ProceedButtonText, contributors.Value.ProceedButtonText, $"Go to {Singular}");

    public string ContributorsCurrentHeading =>
        Text(Copy?.Contributors?.CurrentHeading, contributors.Value.CurrentHeading, "Current contributors");

    public string ReviewButtonText =>
        Text(Copy?.TaskList?.ReviewButtonText, taskList.Value.ReviewButtonText, $"Review {Singular}");

    public string ViewButtonText =>
        Text(Copy?.TaskList?.ViewButtonText, taskList.Value.ViewButtonText, $"View {Singular}");

    private TemplateDisplayCopy? Copy => templateCopy.Template?.DisplayCopy;

    private string? TemplateName => templateCopy.Template?.TemplateName;

    private string Text(string? templateValue, string? tenantValue, string codeDefault) =>
        DisplayCopyPlaceholders.Apply(
            First(templateValue, tenantValue, codeDefault),
            TemplateName,
            Singular,
            Plural,
            configuration["Layout:ContributorEmail"]);

    private static string First(string? templateValue, string? tenantValue, string? codeDefault = null)
    {
        if (!string.IsNullOrWhiteSpace(templateValue))
            return templateValue.Trim();

        if (!string.IsNullOrWhiteSpace(tenantValue))
            return tenantValue.Trim();

        return codeDefault?.Trim() ?? string.Empty;
    }

    private static string Capitalise(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpper(value[0]) + value[1..];
}
