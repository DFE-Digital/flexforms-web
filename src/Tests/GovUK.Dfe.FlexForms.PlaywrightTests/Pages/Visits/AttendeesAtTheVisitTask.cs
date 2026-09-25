using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Visits;

public sealed class AttendeesAtTheVisitTask(IPage page) : TaskPage(page)
{
    protected override string TaskItem => "group-engagement-details-task-attendees-at-the-visit";

    public async Task CompleteAsync() => await MarkCompleteAndSaveAsync();

    public async Task AddLeadAttendeeAsync(string name, string role) => await AddAttendeeAsync("leadAttendee", name, role);

    public async Task AddDfEAttendeeAsync(string name, string role) => await AddAttendeeAsync("dfeAttendee", name, role, "s");

    public async Task AddExternalOrganisationAttendeeAsync(string name, string role) =>
        await AddAttendeeAsync("externalOrganisationAttendee", name, role, "s");

    private async Task AddAttendeeAsync(string section, string name, string role, string addItemAdditional = "")
    {
        var nameParts = name.Split(' ');

        await ById($"{section}{addItemAdditional}-add-item").ClickAsync();
        await ByIdData($"{section}FirstName").FillAsync(nameParts[0]);
        await ByIdData($"{section}LastName").FillAsync(nameParts[1]);
        await ByIdData($"{section}JobTitle").FillAsync(role);
        await SaveAndContinueAsync();
    }
}
