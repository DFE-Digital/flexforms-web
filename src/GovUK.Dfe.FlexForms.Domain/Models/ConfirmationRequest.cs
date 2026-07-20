using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models
{
    /// <summary>
    /// Represents a request for confirmation of a user action
    /// </summary>
    public class ConfirmationRequest
    {
        /// <summary>
        /// Unique token identifying this confirmation request
        /// </summary>
        [JsonPropertyName("confirmationToken")]
        public string ConfirmationToken { get; set; } = string.Empty;

        /// <summary>
        /// The original page path that initiated the confirmation
        /// </summary>
        [JsonPropertyName("originalPagePath")]
        public string OriginalPagePath { get; set; } = string.Empty;

        /// <summary>
        /// The original handler that was being called
        /// </summary>
        [JsonPropertyName("originalHandler")]
        public string OriginalHandler { get; set; } = string.Empty;

        /// <summary>
        /// The original form data that was submitted
        /// </summary>
        [JsonPropertyName("originalFormData")]
        public Dictionary<string, object> OriginalFormData { get; set; } = new();

        /// <summary>
        /// The fields to display on the confirmation page
        /// </summary>
        [JsonPropertyName("displayFields")]
        public string[] DisplayFields { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Optional custom title/heading to show on the confirmation page
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; }

        /// <summary>
        /// Optional custom error message to show on the confirmation page if the user doesn't select a value
        /// </summary>
        [JsonPropertyName("requiredMessage")]
        public string? RequiredMessage { get; set; }

        /// <summary>
        /// The URL to return to if the user cancels
        /// </summary>
        [JsonPropertyName("returnUrl")]
        public string ReturnUrl { get; set; } = string.Empty;

        /// <summary>
        /// When this confirmation request was created
        /// </summary>
        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

