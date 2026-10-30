using GovUK.Dfe.FlexForms.Application.DisplayCopy;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Domain.Models;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Application.Tests.DisplayCopy;

public class TemplateDisplayCopyResolverTests
{
    [Fact]
    public void Resolve_UsesCodeDefaults_WhenTemplateAndTenantAreBlank()
    {
        var copy = Create(new TemplateCopyContext(), new ApplicationTerminologyOptions());

        Assert.Equal("application", copy.Singular);
        Assert.Equal("Your applications", copy.DashboardMainHeading);
        Assert.Equal("Contributors", copy.ContributorsSectionHeading);
        Assert.Equal("Invite contributors", copy.ContributorsInviteButtonText);
        Assert.Equal("Review application", copy.ReviewButtonText);
        Assert.Equal("View application", copy.ViewButtonText);
    }

    [Fact]
    public void Resolve_TemplateValueWins_OverTenantSetting()
    {
        var context = new TemplateCopyContext();
        context.Use(Template(new TemplateDisplayCopy
        {
            Terminology = new TemplateTerminologyCopy { Singular = "visit", Plural = "visits" },
            Dashboard = new TemplateDashboardCopy { MainHeading = "Your {plural}" },
            TaskList = new TemplateTaskListCopy { ReviewButtonText = "Check this {singular}" }
        }));

        var copy = Create(
            context,
            new ApplicationTerminologyOptions { Singular = "application", Plural = "applications" },
            dashboard: new DashboardOptions { MainHeading = "Tenant heading" },
            taskList: new TaskListOptions { ReviewButtonText = "Tenant review" });

        Assert.Equal("visit", copy.Singular);
        Assert.Equal("Your visits", copy.DashboardMainHeading);
        Assert.Equal("Check this visit", copy.ReviewButtonText);
        Assert.Equal("View visit", copy.ViewButtonText);
    }

    [Fact]
    public void Resolve_BlankTemplateField_InheritsTenantSetting()
    {
        var context = new TemplateCopyContext();
        context.Use(Template(new TemplateDisplayCopy
        {
            Contributors = new TemplateContributorsCopy { SectionHeading = "People" }
        }));

        var copy = Create(
            context,
            new ApplicationTerminologyOptions(),
            contributors: new ContributorsOptions { InviteButtonText = "Share this form" });

        Assert.Equal("People", copy.ContributorsSectionHeading);
        Assert.Equal("Share this form", copy.ContributorsInviteButtonText);
        Assert.Equal(
            "You can invite other people to complete or view parts of this application.",
            copy.ContributorsSectionBody);
    }

    [Fact]
    public void Resolve_UsesTenantPreviewAndTaskList_WhenTemplateFieldsAreBlank()
    {
        var copy = Create(
            new TemplateCopyContext(),
            new ApplicationTerminologyOptions { Singular = "plan", Plural = "plans" },
            dashboard: new DashboardOptions(),
            contributors: new ContributorsOptions(),
            taskList: new TaskListOptions
            {
                ReviewButtonText = "Check {singular}",
                ViewButtonText = "Open {singular}"
            },
            preview: new ApplicationPreviewOptions
            {
                PageHeading = "Review your {singular}",
                SubmitHeading = "Send {singular}",
                SubmitHint = "Confirm",
                SubmitButtonText = "Send now"
            });

        Assert.Equal("Review your plan", copy.PreviewPageHeading);
        Assert.Equal("Send plan", copy.PreviewSubmitHeading);
        Assert.Equal("Confirm", copy.PreviewSubmitHint);
        Assert.Equal("Send now", copy.PreviewSubmitButtonText);
        Assert.Equal("Check plan", copy.ReviewButtonText);
        Assert.Equal("Open plan", copy.ViewButtonText);
    }

    [Fact]
    public void Resolve_SubstitutesContributorEmail_InSignInText()
    {
        var context = new TemplateCopyContext();
        context.Use(Template(new TemplateDisplayCopy
        {
            Contributors = new TemplateContributorsCopy
            {
                SignInText = "Use a business email like {contributor_email}."
            }
        }));

        var copy = Create(context, new ApplicationTerminologyOptions());

        Assert.Equal("Use a business email like name@school.sch.uk.", copy.ContributorsSignInText);
    }

    [Fact]
    public void Resolve_ExposesCapitalisedTerminology_FromResolvedSingularAndPlural()
    {
        var copy = Create(
            new TemplateCopyContext(),
            new ApplicationTerminologyOptions { Singular = "visit", Plural = "visits" });

        Assert.Equal("Visit", copy.SingularCapitalised);
        Assert.Equal("Visits", copy.PluralCapitalised);
        Assert.Equal("Visits in progress", copy.DashboardInProgressHeading);
    }

    [Fact]
    public void Resolve_UsesTenantDashboardAndContributorDefaults_WhenTemplateCopyIsMissing()
    {
        var copy = Create(
            new TemplateCopyContext(),
            new ApplicationTerminologyOptions { Singular = "plan", Plural = "plans" },
            dashboard: new DashboardOptions
            {
                MainHeading = "Tenant {plural}",
                InProgressHeading = "Tenant in progress",
                StartNewHeading = "Tenant start",
                StartNewHint = "Tenant hint",
                StartNewButtonText = "Tenant button"
            },
            contributors: new ContributorsOptions
            {
                SectionHeading = "Helpers",
                SectionBody = "Body for {singular}",
                InviteButtonText = "Invite",
                PageHeading = "Invite page",
                Intro = "Intro for {singular}",
                SignInText = "Sign in",
                OnceJoinedText = "Joined {singular}",
                AddLaterText = "Later",
                AddButtonText = "Add",
                ProceedButtonText = "Proceed",
                CurrentHeading = "Current"
            });

        Assert.Equal("Tenant plans", copy.DashboardMainHeading);
        Assert.Equal("Tenant in progress", copy.DashboardInProgressHeading);
        Assert.Equal("Tenant start", copy.DashboardStartNewHeading);
        Assert.Equal("Tenant hint", copy.DashboardStartNewHint);
        Assert.Equal("Tenant button", copy.DashboardStartNewButtonText);
        Assert.Equal("Helpers", copy.ContributorsSectionHeading);
        Assert.Equal("Body for plan", copy.ContributorsSectionBody);
        Assert.Equal("Invite", copy.ContributorsInviteButtonText);
        Assert.Equal("Invite page", copy.ContributorsPageHeading);
        Assert.Equal("Intro for plan", copy.ContributorsIntro);
        Assert.Equal("Sign in", copy.ContributorsSignInText);
        Assert.Equal("Joined plan", copy.ContributorsOnceJoinedText);
        Assert.Equal("Later", copy.ContributorsAddLaterText);
        Assert.Equal("Add", copy.ContributorsAddButtonText);
        Assert.Equal("Proceed", copy.ContributorsProceedButtonText);
        Assert.Equal("Current", copy.ContributorsCurrentHeading);
    }

    [Fact]
    public void Resolve_UsesTemplateCopyForEverySection_AndSubstitutesTemplateName()
    {
        var context = new TemplateCopyContext();
        context.Use(Template(new TemplateDisplayCopy
        {
            Terminology = new TemplateTerminologyCopy { Singular = "case", Plural = "cases" },
            Dashboard = new TemplateDashboardCopy
            {
                MainHeading = "{template_name} dashboard",
                InProgressHeading = "In progress",
                StartNewHeading = "Start",
                StartNewHint = "Hint",
                StartNewButtonText = "Go"
            },
            Preview = new TemplatePreviewCopy
            {
                PageHeading = "Preview",
                SubmitHeading = "Send",
                SubmitHint = "Sure?",
                SubmitButtonText = "Send now"
            },
            Contributors = new TemplateContributorsCopy
            {
                SectionHeading = "Team",
                SectionBody = "Help with {template_name}",
                InviteButtonText = "Add team",
                PageHeading = "Invite team",
                Intro = "Intro",
                SignInText = "Sign in",
                OnceJoinedText = "Joined",
                AddLaterText = "Later",
                AddButtonText = "Add",
                ProceedButtonText = "Back",
                CurrentHeading = "Current team"
            },
            TaskList = new TemplateTaskListCopy
            {
                ReviewButtonText = "Review {singular}",
                ViewButtonText = "Open {singular}"
            }
        }));

        var copy = Create(context, new ApplicationTerminologyOptions());

        Assert.Equal("Visits dashboard", copy.DashboardMainHeading);
        Assert.Equal("In progress", copy.DashboardInProgressHeading);
        Assert.Equal("Preview", copy.PreviewPageHeading);
        Assert.Equal("Help with Visits", copy.ContributorsSectionBody);
        Assert.Equal("Review case", copy.ReviewButtonText);
        Assert.Equal("Open case", copy.ViewButtonText);
        Assert.Equal("Team", copy.ContributorsSectionHeading);
        Assert.Equal("Current team", copy.ContributorsCurrentHeading);
    }

    [Fact]
    public void Resolve_TrimsWhitespace_WhenChoosingBetweenTemplateTenantAndDefaults()
    {
        var context = new TemplateCopyContext();
        context.Use(Template(new TemplateDisplayCopy
        {
            Terminology = new TemplateTerminologyCopy { Singular = "  visit  ", Plural = "  visits  " }
        }));

        var copy = Create(context, new ApplicationTerminologyOptions());

        Assert.Equal("visit", copy.Singular);
        Assert.Equal("visits", copy.Plural);
    }

    private static TemplateDisplayCopyResolver Create(
        ITemplateCopyContext context,
        ApplicationTerminologyOptions terminology,
        DashboardOptions? dashboard = null,
        ContributorsOptions? contributors = null,
        TaskListOptions? taskList = null,
        ApplicationPreviewOptions? preview = null)
    {
        var configuration = Substitute.For<IRequestAppConfiguration>();
        configuration["Layout:ContributorEmail"].Returns("name@school.sch.uk");
        return new TemplateDisplayCopyResolver(
            context,
            Microsoft.Extensions.Options.Options.Create(terminology),
            Microsoft.Extensions.Options.Options.Create(dashboard ?? new DashboardOptions()),
            Microsoft.Extensions.Options.Options.Create(preview ?? new ApplicationPreviewOptions()),
            Microsoft.Extensions.Options.Options.Create(contributors ?? new ContributorsOptions()),
            Microsoft.Extensions.Options.Options.Create(taskList ?? new TaskListOptions()),
            configuration);
    }

    private static FormTemplate Template(TemplateDisplayCopy copy) =>
        new()
        {
            TemplateId = "form-001",
            TemplateName = "Visits",
            Description = "Visits",
            TaskGroups = [],
            DisplayCopy = copy
        };
}
