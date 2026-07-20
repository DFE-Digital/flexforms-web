namespace GovUK.Dfe.FlexForms.Domain.Models
{
    public class ComplexFieldConfiguration
    {
        public string Id { get; set; } = string.Empty;
        public string ApiEndpoint { get; set; } = string.Empty;
        public string ApiKey { get; set; } = string.Empty;
        public string FieldType { get; set; } = "autocomplete"; // autocomplete, composite, etc.
        public bool AllowMultiple { get; set; } = false;
        public int MinLength { get; set; } = 3;
        public string Placeholder { get; set; } = "Start typing to search...";
        public int MaxSelections { get; set; } = 0; // 0 means no limit
        public string Label { get; set; } = "Item"; // Default label for the field
        public Dictionary<string, object> AdditionalProperties { get; set; } = new(); // For field-specific config
    }
} 