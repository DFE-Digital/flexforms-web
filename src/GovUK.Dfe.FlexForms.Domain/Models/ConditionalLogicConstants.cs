using System.Diagnostics.CodeAnalysis;

namespace GovUK.Dfe.FlexForms.Domain.Models;

/// <summary>
/// Constants for conditional logic system
/// </summary>
[ExcludeFromCodeCoverage]
public static class ConditionalLogicConstants
{
    /// <summary>
    /// Supported comparison operators
    /// </summary>
    public static class Operators
    {
        // Basic comparison
        public new const string Equals = "equals";
        public const string NotEquals = "notEquals";
        public const string In = "in";
        public const string NotIn = "notIn";
        public const string Contains = "contains";
        public const string StartsWith = "startsWith";
        public const string EndsWith = "endsWith";

        // Numeric comparison
        public const string GreaterThan = "greaterThan";
        public const string LessThan = "lessThan";
        public const string GreaterThanOrEqual = "greaterThanOrEqual";
        public const string LessThanOrEqual = "lessThanOrEqual";
        public const string Between = "between";

        // Date comparison
        public const string Before = "before";
        public const string After = "after";
        public const string DateBetween = "dateBetween";
        public const string IsToday = "isToday";
        public const string IsInPast = "isInPast";
        public const string IsInFuture = "isInFuture";

        // Boolean and state
        public const string IsTrue = "isTrue";
        public const string IsFalse = "isFalse";
        public const string IsEmpty = "isEmpty";
        public const string IsNotEmpty = "isNotEmpty";

        // Complex operations
        public const string HasLength = "hasLength";
        public const string MatchesPattern = "matchesPattern";
        public const string IsValidEmail = "isValidEmail";
        public const string IsValidPhone = "isValidPhone";
    }

    /// <summary>
    /// Supported logical operators
    /// </summary>
    public static class LogicalOperators
    {
        public const string And = "AND";
        public const string Or = "OR";
        public const string Not = "NOT";
    }

    /// <summary>
    /// Supported element types
    /// </summary>
    public static class ElementTypes
    {
        public const string Field = "field";
        public const string Page = "page";
        public const string FieldGroup = "fieldGroup";
        public const string Task = "task";
        public const string Section = "section";
    }

    /// <summary>
    /// Supported actions
    /// </summary>
    public static class Actions
    {
        // Visibility actions
        public const string Show = "show";
        public const string Hide = "hide";
        public const string Skip = "skip";

        // Requirement actions
        public const string Require = "require";
        public const string MakeOptional = "makeOptional";

        // State actions
        public const string Enable = "enable";
        public const string Disable = "disable";

        // Value actions
        public const string SetValue = "setValue";
        public const string ClearValue = "clearValue";

        // Validation actions
        public const string AddValidation = "addValidation";
        public const string RemoveValidation = "removeValidation";

        // Navigation actions
        public const string Redirect = "redirect";
        public const string ShowMessage = "showMessage";
    }

    /// <summary>
    /// Supported data types
    /// </summary>
    public static class DataTypes
    {
        public const string String = "string";
        public const string Number = "number";
        public const string Boolean = "boolean";
        public const string Date = "date";
        public const string Array = "array";
        public const string Object = "object";
    }

    /// <summary>
    /// Execution triggers
    /// </summary>
    public static class ExecutionTriggers
    {
        public const string Change = "change";
        public const string Load = "load";
        public const string Submit = "submit";
        public const string Focus = "focus";
        public const string Blur = "blur";
    }
}
