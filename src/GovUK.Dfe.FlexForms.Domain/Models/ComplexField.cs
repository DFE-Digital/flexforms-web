using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace GovUK.Dfe.FlexForms.Domain.Models
{
    public class ComplexField
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Expression for each autocomplete dropdown row, e.g. <c>displayName + " - " + constituencyName</c>.
        /// Overrides the matching tenant setting when set. Empty keeps the built-in dropdown layout.
        /// </summary>
        [JsonPropertyName("dropdownDisplay")]
        public string? DropdownDisplay { get; set; }

        /// <summary>
        /// Expression for the check-your-answers / confirmation value, using the same syntax as <see cref="DropdownDisplay"/>.
        /// Overrides the matching tenant setting when set. Empty keeps the built-in confirmation layout.
        /// </summary>
        [JsonPropertyName("confirmationDisplay")]
        public string? ConfirmationDisplay { get; set; }
    }
}
