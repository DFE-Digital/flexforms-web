using System.Text;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Domain.Templates;

/// <summary>
/// Builds a minimal valid form schema so newly created templates can render the dashboard
/// without requiring an immediate manual schema upload.
/// </summary>
public static class StarterFormTemplateSchema
{
    public const string DefaultVersionNumber = "1.0.0";

    /// <summary>
    /// Creates a starter <see cref="FormTemplate"/> for the given template id and display name.
    /// </summary>
    public static FormTemplate Create(string templateId, string templateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateId);
        ArgumentException.ThrowIfNullOrWhiteSpace(templateName);

        return new FormTemplate
        {
            TemplateId = templateId,
            TemplateName = templateName.Trim(),
            Description = "Starter template. Edit this schema in Template Manager.",
            DefaultFieldRequirementPolicy = "optional",
            ContributorPattern = false,
            TaskGroups =
            [
                new TaskGroup
                {
                    GroupId = "starter-group",
                    GroupName = "Getting started",
                    GroupOrder = 1,
                    GroupStatus = nameof(Models.TaskStatus.NotStarted),
                    Tasks =
                    [
                        new Models.Task
                        {
                            TaskId = "starter-task",
                            TaskName = "About this form",
                            TaskOrder = 1,
                            TaskStatusString = nameof(Models.TaskStatus.NotStarted),
                            StartAtFirstPageWhenNotStarted = true,
                            VisibleInTaskList = true,
                            Pages =
                            [
                                new Page
                                {
                                    PageId = "starter-page",
                                    Slug = "getting-started",
                                    Title = "Getting started",
                                    Description = "Replace this page with your own content.",
                                    PageOrder = 1,
                                    Fields =
                                    [
                                        new Field
                                        {
                                            FieldId = "starter-text",
                                            Type = "text",
                                            Order = 1,
                                            Required = false,
                                            Placeholder = "Enter some text",
                                            Label = new Label
                                            {
                                                Value = "Example text field",
                                                IsVisible = true
                                            }
                                        }
                                    ]
                                }
                            ]
                        }
                    ]
                }
            ]
        };
    }

    /// <summary>
    /// Serializes the starter template to UTF-8 JSON.
    /// </summary>
    public static string CreateJson(string templateId, string templateName) =>
        JsonSerializer.Serialize(Create(templateId, templateName));

    /// <summary>
    /// Serializes the starter template to Base64 (API schema transport format).
    /// </summary>
    public static string CreateBase64Json(string templateId, string templateName) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(CreateJson(templateId, templateName)));
}
