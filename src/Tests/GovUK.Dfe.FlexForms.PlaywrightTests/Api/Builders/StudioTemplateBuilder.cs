using System.Text.Json;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api.Builders;

/// <summary>
/// The known version the Template Studio tests start from: a task with three pages and a task with a repeatable list.
/// No applications use the template, so a new version can replace anything in it.
/// </summary>
public static class StudioTemplateBuilder
{
    public const string SectionName = "Studio section";

    public const string DetailsTask = "Studio details";
    public const string HasTrustPage = "Is there a trust?";
    public const string HasTrustQuestionId = "hasTrust";
    public const string HasTrustRequiredMessage = "Select yes if there is a trust";
    public const string TrustNamePage = "Trust name";
    public const string TrustNameQuestionId = "trustName";
    public const string NotesPage = "Notes";

    public const string ContactsTask = "Studio contacts";
    public const string ContactsList = "Contacts";
    public const string ContactsListId = "contacts";
    public const string ContactNameQuestion = "Contact name";
    public const string ContactNameQuestionId = "contactName";
    public const string AddContactButton = "Add a contact";
    public const string ContactPage = "Contact";

    public static string Schema(string templateId, string templateName) => JsonSerializer.Serialize(new
    {
        templateId,
        templateName,
        description = "Used by the Template Studio Playwright tests.",
        taskGroups = new[]
        {
            new
            {
                groupId = "studio-section",
                groupName = SectionName,
                groupOrder = 1,
                groupStatus = "NotStarted",
                tasks = new object[]
                {
                    new
                    {
                        taskId = "studio-details",
                        taskName = DetailsTask,
                        taskOrder = 1,
                        taskStatus = "NotStarted",
                        pages = new[]
                        {
                            Page("has-trust", HasTrustPage, 1, Radios(HasTrustQuestionId, HasTrustPage, HasTrustRequiredMessage)),
                            Page("trust-name", TrustNamePage, 2, Text(TrustNameQuestionId, TrustNamePage, "Enter the trust name")),
                            Page("notes", NotesPage, 3, new { fieldId = "notes", type = "text-area", order = 1, required = false, label = new { value = NotesPage } })
                        }
                    },
                    new
                    {
                        taskId = "studio-contacts",
                        taskName = ContactsTask,
                        taskOrder = 2,
                        taskStatus = "NotStarted",
                        pages = (object?)null,
                        summary = new
                        {
                            mode = "multiCollectionFlow",
                            flows = new[]
                            {
                                new
                                {
                                    flowId = "contacts-flow",
                                    title = ContactsList,
                                    fieldId = ContactsListId,
                                    addButtonLabel = AddContactButton,
                                    itemKind = "contact",
                                    itemKindPlural = "contacts",
                                    itemTitleBinding = ContactNameQuestionId,
                                    minItems = 1,
                                    tableType = "card",
                                    addItemMessage = "{contactName} has been added",
                                    deleteItemMessage = "{contactName} has been removed",
                                    pages = new[]
                                    {
                                        Page("contact", ContactPage, 1, Text(ContactNameQuestionId, ContactNameQuestion, "Enter the contact's name"))
                                    }
                                }
                            }
                        }
                    }
                }
            }
        },
        conditionalLogic = Array.Empty<object>()
    });

    /// <summary>The version after <paramref name="latest"/>, for example 1.0.4 after 1.0.3.</summary>
    public static string NextVersion(string? latest)
    {
        var parts = (latest ?? "1.0.0").Split('.').Select(part => int.TryParse(part, out var number) ? number : 0).ToList();
        while (parts.Count < 3)
            parts.Add(0);
        parts[2]++;
        return string.Join('.', parts.Take(3));
    }

    // Pages go on to the next page rather than back to the summary, so tests can follow the rules from page to page.
    private static object Page(string pageId, string title, int order, object field) => new
    {
        pageId,
        slug = pageId,
        title,
        description = "",
        pageOrder = order,
        returnToSummaryPage = false,
        fields = new[] { field }
    };

    private static object Radios(string fieldId, string label, string requiredMessage) => new
    {
        fieldId,
        type = "radios",
        order = 1,
        label = new { value = label },
        options = new[] { new { value = "yes", label = "Yes" }, new { value = "no", label = "No" } },
        validations = new[] { new { type = "required", rule = (object)true, message = requiredMessage } }
    };

    private static object Text(string fieldId, string label, string requiredMessage) => new
    {
        fieldId,
        type = "text",
        order = 1,
        label = new { value = label },
        validations = new[] { new { type = "required", rule = (object)true, message = requiredMessage } }
    };
}
