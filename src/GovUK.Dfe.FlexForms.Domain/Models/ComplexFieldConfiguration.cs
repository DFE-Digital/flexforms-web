namespace GovUK.Dfe.FlexForms.Domain.Models
{
    public class ComplexFieldConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string ApiEndpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        /// <summary>
        /// Authentication for the third-party autocomplete API.
        /// <c>ApiKey</c> (default) or <c>ClientCredentials</c>.
        /// When empty, client credentials are used if TokenEndpoint, ClientId and ClientSecret are set.
        /// </summary>
        public string AuthType { get; set; } = string.Empty;
        public string TokenEndpoint { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string FieldType { get; set; } = "autocomplete"; // autocomplete, composite, etc.
        public bool AllowMultiple { get; set; } = false;
        public int MinLength { get; set; } = 3;
        public string Placeholder { get; set; } = "Start typing to search...";
        public int MaxSelections { get; set; } = 0; // 0 means no limit
        public string Label { get; set; } = "Item"; // Default label for the field
        public Dictionary<string, object> AdditionalProperties { get; set; } = new(); // For field-specific config

        public bool UsesClientCredentials =>
            string.Equals(AuthType, "ClientCredentials", StringComparison.OrdinalIgnoreCase)
            || (string.IsNullOrWhiteSpace(AuthType)
                && !string.IsNullOrWhiteSpace(TokenEndpoint)
                && !string.IsNullOrWhiteSpace(ClientId)
                && !string.IsNullOrWhiteSpace(ClientSecret));
    }
} 