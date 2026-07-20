using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models
{
    /// <summary>
    /// Represents the context of a confirmation request including expiry information
    /// </summary>
    public class ConfirmationContext
    {
        /// <summary>
        /// The unique token for this confirmation
        /// </summary>
        [JsonPropertyName("token")]
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// The confirmation request details
        /// </summary>
        [JsonPropertyName("request")]
        public ConfirmationRequest Request { get; set; } = new();

        /// <summary>
        /// When this confirmation context expires
        /// </summary>
        [JsonPropertyName("expiresAt")]
        public DateTime ExpiresAt { get; set; }

        /// <summary>
        /// Whether this confirmation context has expired
        /// </summary>
        [JsonIgnore]
        public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    }
}

