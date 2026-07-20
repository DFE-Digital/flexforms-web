using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Domain.Models;
using System.Diagnostics.CodeAnalysis;
using Task = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Web.Services
{
    [ExcludeFromCodeCoverage]
    public class FieldViewModel
    {
        public Field Field { get; }
        public string Prefix { get; }
        public string CurrentValue { get; }
        public IReadOnlyCollection<string> SelectedValues { get; }
        public string ErrorMessage { get; }
        public string TaskName => CurrentTask.TaskName;
        public Page CurrentPage { get; }
        public Task CurrentTask { get; }

        public FieldViewModel(Field field, string prefix, string currentValue, string errorMessage, Task currentTask, Page currentPage, IReadOnlyCollection<string>? selectedValues = null)
        {
            Field = field;
            Prefix = prefix;
            CurrentValue = currentValue;
            SelectedValues = selectedValues ?? Array.Empty<string>();
            ErrorMessage = errorMessage;
            CurrentTask = currentTask;
            CurrentPage = currentPage;
        }

        public string Name => $"{Prefix}[{Field.FieldId}]";
        public string Id => $"{Prefix}_{Field.FieldId}";

        // Property to format autocomplete object values for display
        public string DisplayValue
        {
            get
            {
                if (string.IsNullOrEmpty(CurrentValue) || Field.Type != "autocomplete")
                {
                    return CurrentValue ?? "";
                }

                try
                {
                    using (var doc = JsonDocument.Parse(CurrentValue))
                    {
                        if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        {
                            var displayValues = new List<string>();
                            foreach (var element in doc.RootElement.EnumerateArray())
                            {
                                displayValues.Add(FormatSingleValue(element));
                            }
                            return string.Join(", ", displayValues);
                        }
                        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
                        {
                            return FormatSingleValue(doc.RootElement);
                        }
                    }
                }
                catch
                {
                    // If not JSON, return as is
                }

                return CurrentValue;
            }
        }

        private string FormatSingleValue(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                string name = "";
                string ukprn = "";

                if (element.TryGetProperty("name", out var nameProperty) && nameProperty.ValueKind == JsonValueKind.String)
                {
                    name = nameProperty.GetString() ?? "";
                }

                if (element.TryGetProperty("ukprn", out var ukprnProperty))
                {
                    if (ukprnProperty.ValueKind == JsonValueKind.String)
                    {
                        ukprn = ukprnProperty.GetString() ?? "";
                    }
                    else if (ukprnProperty.ValueKind == JsonValueKind.Number)
                    {
                        ukprn = ukprnProperty.GetInt64().ToString();
                    }
                }

                if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(ukprn))
                {
                    return $"{name} (UKPRN: {ukprn})";
                }
                else if (!string.IsNullOrEmpty(name))
                {
                    return name;
                }
            }
            else if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString() ?? "";
            }

            return element.ToString();
        }

        // Builds data-val attributes for client-side validation, including conditional rules
        public string ValidationAttributes
        {
            get
            {
                if (Field.Validations == null || !Field.Validations.Any())
                    return string.Empty;

                var sb = new StringBuilder();
                sb.Append(" data-val=\"true\"");

                foreach (var v in Field.Validations)
                {
                    // Emit conditional metadata if needed
                    if (v.Condition != null)
                    {
                        sb.Append($" data-val-cond-field=\"{v.Condition.TriggerField}\"");
                        sb.Append($" data-val-cond-operator=\"{v.Condition.Operator}\"");
                        sb.Append($" data-val-cond-value=\"{v.Condition.Value}\"");
                    }

                    switch (v.Type)
                    {
                        case "required":
                            sb.Append($" data-val-required=\"{v.Message}\"");
                            break;
                        case "regex":
                            sb.Append($" data-val-regex=\"{v.Message}\" data-val-regex-pattern=\"{v.Rule}\"");
                            break;
                        case "maxLength":
                            sb.Append($" data-val-maxlength=\"{v.Message}\" data-val-maxlength-max=\"{v.Rule}\"");
                            break;
                    }
                }

                return sb.ToString();
            }
        }
    }
}
