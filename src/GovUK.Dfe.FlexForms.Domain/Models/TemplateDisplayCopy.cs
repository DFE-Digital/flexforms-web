using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models;

/// <summary>
/// Optional wording stored on the template. A blank field keeps the tenant setting, then the built-in sentence.
/// </summary>
[ExcludeFromCodeCoverage]
public class TemplateDisplayCopy
{
    [JsonPropertyName("terminology")]
    public TemplateTerminologyCopy? Terminology { get; set; }

    [JsonPropertyName("dashboard")]
    public TemplateDashboardCopy? Dashboard { get; set; }

    [JsonPropertyName("preview")]
    public TemplatePreviewCopy? Preview { get; set; }

    [JsonPropertyName("contributors")]
    public TemplateContributorsCopy? Contributors { get; set; }

    [JsonPropertyName("taskList")]
    public TemplateTaskListCopy? TaskList { get; set; }
}

[ExcludeFromCodeCoverage]
public class TemplateTerminologyCopy
{
    [JsonPropertyName("singular")]
    public string? Singular { get; set; }

    [JsonPropertyName("plural")]
    public string? Plural { get; set; }
}

[ExcludeFromCodeCoverage]
public class TemplateDashboardCopy
{
    [JsonPropertyName("mainHeading")]
    public string? MainHeading { get; set; }

    [JsonPropertyName("inProgressHeading")]
    public string? InProgressHeading { get; set; }

    [JsonPropertyName("startNewHeading")]
    public string? StartNewHeading { get; set; }

    [JsonPropertyName("startNewHint")]
    public string? StartNewHint { get; set; }

    [JsonPropertyName("startNewButtonText")]
    public string? StartNewButtonText { get; set; }
}

[ExcludeFromCodeCoverage]
public class TemplatePreviewCopy
{
    [JsonPropertyName("pageHeading")]
    public string? PageHeading { get; set; }

    [JsonPropertyName("submitHeading")]
    public string? SubmitHeading { get; set; }

    [JsonPropertyName("submitHint")]
    public string? SubmitHint { get; set; }

    [JsonPropertyName("submitButtonText")]
    public string? SubmitButtonText { get; set; }
}

[ExcludeFromCodeCoverage]
public class TemplateContributorsCopy
{
    [JsonPropertyName("sectionHeading")]
    public string? SectionHeading { get; set; }

    [JsonPropertyName("sectionBody")]
    public string? SectionBody { get; set; }

    [JsonPropertyName("inviteButtonText")]
    public string? InviteButtonText { get; set; }

    [JsonPropertyName("pageHeading")]
    public string? PageHeading { get; set; }

    [JsonPropertyName("intro")]
    public string? Intro { get; set; }

    [JsonPropertyName("signInText")]
    public string? SignInText { get; set; }

    [JsonPropertyName("onceJoinedText")]
    public string? OnceJoinedText { get; set; }

    [JsonPropertyName("addLaterText")]
    public string? AddLaterText { get; set; }

    [JsonPropertyName("addButtonText")]
    public string? AddButtonText { get; set; }

    [JsonPropertyName("proceedButtonText")]
    public string? ProceedButtonText { get; set; }

    [JsonPropertyName("currentHeading")]
    public string? CurrentHeading { get; set; }
}

[ExcludeFromCodeCoverage]
public class TemplateTaskListCopy
{
    [JsonPropertyName("reviewButtonText")]
    public string? ReviewButtonText { get; set; }

    [JsonPropertyName("viewButtonText")]
    public string? ViewButtonText { get; set; }
}
