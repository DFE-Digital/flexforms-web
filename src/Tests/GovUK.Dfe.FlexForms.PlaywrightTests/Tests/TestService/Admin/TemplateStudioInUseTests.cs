using System.Text.Json.Nodes;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api;
using GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;
using GovUK.Dfe.FlexForms.PlaywrightTests.Infrastructure;
using GovUK.Dfe.FlexForms.PlaywrightTests.Pages.Admin;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Tests.TestService.Admin;

/// <summary>
/// Uses the shared default template, which applications already use. Nothing is saved.
/// </summary>
[TestFixture(Description = "Template Studio with a template in use")]
public sealed class TemplateStudioInUseTests : PlaywrightTestBase
{
    [OneTimeSetUp]
    public async Task MakeSureApplicationsUseTheTemplateAsync() =>
        await ApplicationApi.CreateApplicationAsync(AdminApiClient, ApplicationBuilder.CreateApplicationRequest(ApiConfig.TemplateId));

    [SetUp]
    public async Task LoginAsAdminAsync() => await LoginAsync("admin");

    [TestCase(TestName = "Deleting a question applications have answered warns that it will be retired")]
    [CiRetry]
    public async Task DeletingAnsweredQuestionWarnsItWillBeRetiredAsync()
    {
        var (taskName, pageTitle, question) = FirstQuestion(await Templates.GetLatestSchemaJsonAsync(AdminApiClient, ApiConfig.TemplateId));
        var studio = new TemplateStudio(Page);
        await studio.OpenAsync(ApiConfig.TemplateId);
        await studio.ExpandAsync(taskName, pageTitle);
        await studio.SelectQuestionAsync(question);

        await studio.StartDeletingAsync();
        await studio.ExpectRetireWarningAsync();

        await studio.CancelDeletingAsync();
        await studio.ExpectQuestionInStructureAsync(question);
    }

    // The first question on a task's pages, named as the structure shows it: a question without a label shows its id.
    private static (string Task, string Page, string Question) FirstQuestion(string json)
    {
        var first = (
                from task in JsonNode.Parse(json)!["taskGroups"]!.AsArray().SelectMany(taskGroup => taskGroup!["tasks"]!.AsArray())
                from page in task!["pages"]?.AsArray() ?? []
                from field in page!["fields"]?.AsArray() ?? []
                select (Task: task, Page: page, Field: field))
            .FirstOrDefault();
        if (first.Field is null)
            throw new InvalidOperationException("The default template has no questions on task pages.");

        var label = first.Field["label"]?["value"]?.GetValue<string>();
        return (first.Task["taskName"]!.GetValue<string>(), first.Page["title"]!.GetValue<string>(),
            string.IsNullOrWhiteSpace(label) ? first.Field["fieldId"]!.GetValue<string>() : label);
    }
}
