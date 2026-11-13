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
        /// Name used in sentences such as "Is this the right local authority?".
        /// When empty, the tenant <c>FormEngine:ComplexFields</c> Label is used.
        /// </summary>
        [JsonPropertyName("label")]
        public string? Label { get; set; }

        /// <summary>
        /// Confirmation heading, for example "Is this the correct {name}?".
        /// <c>{name}</c> is the sentence name. When empty, "Is this the right {name}?" is used.
        /// </summary>
        [JsonPropertyName("confirmationTitle")]
        public string? ConfirmationTitle { get; set; }

        /// <summary>
        /// Expression for each autocomplete dropdown row, e.g. <c>displayName + " - " + constituencyName</c>.
        /// Overrides the matching tenant setting when set. Empty keeps the built-in dropdown layout.
        /// Supports Markdown (line breaks, bold, etc.).
        /// </summary>
        [JsonPropertyName("dropdownDisplay")]
        public string? DropdownDisplay { get; set; }

        /// <summary>
        /// Expression for the “Is this the right …?” confirmation inset. When set, fully replaces the
        /// confirmation display box (not individual property rows). Same expression syntax as
        /// <see cref="DropdownDisplay"/>. Supports Markdown.
        /// </summary>
        [JsonPropertyName("confirmationDisplay")]
        public string? ConfirmationDisplay { get; set; }

        /// <summary>
        /// Expression for task / collection summary values. When set, fully replaces the built-in
        /// summary layout for this field. Same expression syntax as <see cref="DropdownDisplay"/>.
        /// Supports Markdown. Independent of <see cref="ConfirmationDisplay"/>.
        /// </summary>
        [JsonPropertyName("summaryDisplay")]
        public string? SummaryDisplay { get; set; }
    }
}
