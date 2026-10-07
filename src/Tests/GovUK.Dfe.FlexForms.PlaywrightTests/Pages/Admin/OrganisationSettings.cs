using GovUK.Dfe.FlexForms.PlaywrightTests.Support;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

public sealed class OrganisationSettings : BasePage
{
    private readonly List<Func<Task>> _changes = [];

    public OrganisationSettings(IPage page, Terminology terminology)
        : base(page, terminology)
    {
        ApplicationTerminology = new ApplicationTerminologySettings(this);
        NotificationBanner = new NotificationBannerSettings(this);
        Dashboard = new DashboardSettings(this);
        CheckYourAnswersSettings = new CheckYourAnswersSettings(this);
        ApplicationSubmittedSettings = new ApplicationSubmittedSettings(this);
    }

    public ApplicationTerminologySettings ApplicationTerminology { get; }

    public NotificationBannerSettings NotificationBanner { get; }

    public ApplicationSubmittedSettings ApplicationSubmittedSettings { get; }

    public CheckYourAnswersSettings CheckYourAnswersSettings { get; }

    public DashboardSettings Dashboard { get; }

    public async Task GoToAsync() => await Page.GotoAsync("/admin/organisation-settings");

    public async Task SaveAsync()
    {
        foreach (var change in _changes)
        {
            await change();
        }

        _changes.Clear();
        await Page.GetByRole(AriaRole.Button, new PageGetByRoleOptions { Name = "Save settings" }).ClickAsync();
        await Assertions.Expect(Page.GetByRole(AriaRole.Alert)).ToContainTextAsync("Organisation settings saved.");
    }

    internal TSection Enqueue<TSection>(TSection section, string fieldId, Func<ILocator, Task> change)
    {
        _changes.Add(() => change(ById(fieldId)));
        return section;
    }
}

public sealed class ApplicationTerminologySettings(OrganisationSettings settings)
{
    public ApplicationTerminologySettings WithSingular(string singular) =>
        settings.Enqueue(this, "TerminologySingular", field => field.FillAsync(singular));

    public ApplicationTerminologySettings WithPlural(string plural) =>
        settings.Enqueue(this, "TerminologyPlural", field => field.FillAsync(plural));

    public Task SaveAsync() => settings.SaveAsync();
}

public sealed class NotificationBannerSettings(OrganisationSettings settings)
{
    public NotificationBannerSettings Enabled() =>
        settings.Enqueue(this, "BannerEnabled", field => field.CheckAsync());

    public NotificationBannerSettings WithHeading(string heading) =>
        settings.Enqueue(this, "BannerHeading", field => field.FillAsync(heading));

    public NotificationBannerSettings WithMessage(string message) =>
        settings.Enqueue(this, "BannerMessage", field => field.FillAsync(message));

    public Task SaveAsync() => settings.SaveAsync();
}

public sealed class DashboardSettings(OrganisationSettings settings)
{
    public DashboardSettings WithPageSize(int pageSize) =>
        settings.Enqueue(this, "DashboardPageSize", field => field.FillAsync(pageSize.ToString()));

    public DashboardSettings WithFiltersEnabled() =>
        settings.Enqueue(this, "DashboardEnableFilters", field => field.CheckAsync());

    public DashboardSettings WithFiltersDisabled() =>
        settings.Enqueue(this, "DashboardEnableFilters", field => field.UncheckAsync());

    public DashboardSettings WithMainHeading(string heading) =>
        settings.Enqueue(this, "DashboardMainHeading", field => field.FillAsync(heading));

    public DashboardSettings WithInProgressHeading(string heading) =>
        settings.Enqueue(this, "DashboardInProgressHeading", field => field.FillAsync(heading));

    public DashboardSettings WithStartNewHeading(string heading) =>
        settings.Enqueue(this, "DashboardStartNewHeading", field => field.FillAsync(heading));

    public DashboardSettings WithStartNewHint(string hint) =>
        settings.Enqueue(this, "DashboardStartNewHint", field => field.FillAsync(hint));

    public DashboardSettings WithStartNewButtonText(string buttonText) =>
        settings.Enqueue(this, "DashboardStartNewButtonText", field => field.FillAsync(buttonText));

    public Task SaveAsync() => settings.SaveAsync();
}

public sealed class CheckYourAnswersSettings(OrganisationSettings settings)
{
    public CheckYourAnswersSettings WithPageHeading(string heading) =>
        settings.Enqueue(this, "PreviewPageHeading", field => field.FillAsync(heading));

    public CheckYourAnswersSettings WithSubmitHeading(string heading) =>
        settings.Enqueue(this, "PreviewSubmitHeading", field => field.FillAsync(heading));

    public CheckYourAnswersSettings WithSubmitHint(string hint) =>
        settings.Enqueue(this, "PreviewSubmitHint", field => field.FillAsync(hint));

    public CheckYourAnswersSettings WithSubmitButtonText(string buttonText) =>
        settings.Enqueue(this, "PreviewSubmitButtonText", field => field.FillAsync(buttonText));

    public Task SaveAsync() => settings.SaveAsync();
}

public sealed class ApplicationSubmittedSettings(OrganisationSettings settings)
{
    public ApplicationSubmittedSettings ForAllTemplates() => this.ForTemplate("_default");

    public ApplicationSubmittedSettings ForTemplate(string templateId) =>
        settings.Enqueue(this, "SubmittedTemplateId", field => field.SelectOptionAsync(templateId));

    public ApplicationSubmittedSettings WithConfirmationTitle(string title) =>
        settings.Enqueue(this, "SubmittedPanelTitle", field => field.FillAsync(title));

    public ApplicationSubmittedSettings WithPageBody(string body) =>
        settings.Enqueue(this, "SubmittedBodyMarkdown", field => field.FillAsync(body));

    public Task SaveAsync() => settings.SaveAsync();
}