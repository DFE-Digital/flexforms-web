using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models
{
    [ExcludeFromCodeCoverage]
    public class FormTemplate
    {
        [JsonPropertyName("templateId")]
        public required string TemplateId { get; set; }

        [JsonPropertyName("templateName")]
        public required string TemplateName { get; set; }

        [JsonPropertyName("description")]
        public required string Description { get; set; }

        [JsonPropertyName("taskGroups")]
        public required List<TaskGroup> TaskGroups { get; set; }

        /// <summary>
        /// Conditional logic rules for this template
        /// </summary>
        [JsonPropertyName("conditionalLogic")]
        public List<ConditionalLogic>? ConditionalLogic { get; set; }

        /// <summary>
        /// Default field requirement policy for the template.
        /// - "required": All fields are required by default unless explicitly marked as optional
        /// - "optional": All fields are optional by default unless explicitly marked as required
        /// If not specified, defaults to "optional" for backward compatibility
        /// </summary>
        [JsonPropertyName("defaultFieldRequirementPolicy")]
        public string? DefaultFieldRequirementPolicy { get; set; }

        [JsonPropertyName("hideFieldLabelWhenOnlyOneField")]
        public bool HideFieldLabelWhenOnlyOneField { get; set; } = true; // Default to true for backward compatibility

        /// <summary>
        /// When true, owners can invite contributors to help complete the application.
        /// When false, contributor pages and links are hidden.
        /// </summary>
        [JsonPropertyName("contributorPattern")]
        public bool ContributorPattern { get; set; } = true;

        /// <summary>
        /// Optional applications-dashboard column layout. When omitted, only the default
        /// system columns are shown. See <see cref="DashboardConfiguration"/>.
        /// </summary>
        [JsonPropertyName("dashboard")]
        public DashboardConfiguration? Dashboard { get; set; }
    }
}
