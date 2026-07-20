using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Service for evaluating conditional logic rules against form data
/// </summary>
public class ConditionalLogicEngine(ILogger<ConditionalLogicEngine> logger) : IConditionalLogicEngine
{
    public ConditionalLogicResult EvaluateRules(IEnumerable<ConditionalLogic> conditionalLogic,
        Dictionary<string, object> formData,
        ConditionalLogicContext? context = null)
    {
        var result = new ConditionalLogicResult();
        context ??= new ConditionalLogicContext();

        try
        {
            // Sort rules by priority (lower numbers execute first)
            var sortedRules = conditionalLogic
                .Where(r => r.Enabled)
                .OrderBy(r => r.Priority)
                .ToList();

            foreach (var rule in sortedRules)
            {
                try
                {
                    result.EvaluatedRules.Add(rule.Id);

                    if (EvaluateRule(rule, formData, context))
                    {
                        // Rule conditions are met, add actions
                        foreach (var element in rule.AffectedElements)
                        {
                            result.Actions.Add(new ConditionalLogicAction
                            {
                                Element = element,
                                RuleId = rule.Id,
                                Priority = rule.Priority
                            });
                        }

                        logger.LogDebug("Conditional logic rule '{RuleId}' evaluated to true", rule.Id);
                    }
                    else
                    {
                        logger.LogDebug("Conditional logic rule '{RuleId}' evaluated to false", rule.Id);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error evaluating conditional logic rule '{RuleId}'", rule.Id);
                    result.Errors.Add($"Error evaluating rule '{rule.Id}': {ex.Message}");
                }
            }

            // Sort actions by priority
            result.Actions = result.Actions.OrderBy(a => a.Priority).ToList();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error evaluating conditional logic rules");
            result.Errors.Add($"Error evaluating rules: {ex.Message}");
        }

        return result;
    }

    public bool EvaluateRule(ConditionalLogic rule,
        Dictionary<string, object> formData,
        ConditionalLogicContext? context = null)
    {
        try
        {
            return EvaluateConditionGroup(rule.ConditionGroup, formData, context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error evaluating rule '{RuleId}'", rule.Id);
            return false;
        }
    }

    public bool EvaluateConditionGroup(ConditionGroup conditionGroup,
        Dictionary<string, object> formData,
        ConditionalLogicContext? context = null)
    {
        if (conditionGroup.Conditions == null || !conditionGroup.Conditions.Any())
        {
            return true;
        }

        var results = new List<bool>();

        foreach (var condition in conditionGroup.Conditions)
        {
            bool conditionResult;

            // Check if this is a nested condition group
            if (condition.Conditions != null && condition.Conditions.Any())
            {
                var nestedGroup = new ConditionGroup
                {
                    LogicalOperator = condition.LogicalOperator ?? ConditionalLogicConstants.LogicalOperators.And,
                    Conditions = condition.Conditions
                };
                conditionResult = EvaluateConditionGroup(nestedGroup, formData, context);
            }
            else
            {
                conditionResult = EvaluateCondition(condition, formData, context);
            }

            results.Add(conditionResult);
        }

        // Apply logical operator
        return conditionGroup.LogicalOperator?.ToUpperInvariant() switch
        {
            ConditionalLogicConstants.LogicalOperators.Or => results.Any(r => r),
            ConditionalLogicConstants.LogicalOperators.Not => !results.All(r => r),
            _ => results.All(r => r) // Default to AND
        };
    }

    public bool EvaluateCondition(Condition condition,
        Dictionary<string, object> formData,
        ConditionalLogicContext? context = null)
    {
        try
        {
            // Get the field value
            if (!formData.TryGetValue(condition.TriggerField, out var fieldValue))
            {
                fieldValue = null;
            }

            // Perform the comparison based on operator
            return condition.Operator.ToLowerInvariant() switch
            {
                ConditionalLogicConstants.Operators.Equals => CompareEquals(fieldValue, condition.Value, condition.DataType),
                ConditionalLogicConstants.Operators.NotEquals => !CompareEquals(fieldValue, condition.Value, condition.DataType),
                ConditionalLogicConstants.Operators.In => CompareIn(fieldValue, condition.Value, condition.DataType),
                ConditionalLogicConstants.Operators.NotIn => !CompareIn(fieldValue, condition.Value, condition.DataType),
                ConditionalLogicConstants.Operators.Contains => CompareContains(fieldValue, condition.Value),
                ConditionalLogicConstants.Operators.StartsWith => CompareStartsWith(fieldValue, condition.Value),
                ConditionalLogicConstants.Operators.EndsWith => CompareEndsWith(fieldValue, condition.Value),
                ConditionalLogicConstants.Operators.GreaterThan => CompareGreaterThan(fieldValue, condition.Value, condition.DataType),
                ConditionalLogicConstants.Operators.LessThan => CompareLessThan(fieldValue, condition.Value, condition.DataType),
                ConditionalLogicConstants.Operators.GreaterThanOrEqual => CompareGreaterThanOrEqual(fieldValue, condition.Value, condition.DataType),
                ConditionalLogicConstants.Operators.LessThanOrEqual => CompareLessThanOrEqual(fieldValue, condition.Value, condition.DataType),
                ConditionalLogicConstants.Operators.Between => CompareBetween(fieldValue, condition.Value, condition.DataType),
                ConditionalLogicConstants.Operators.IsEmpty => CompareIsEmpty(fieldValue),
                ConditionalLogicConstants.Operators.IsNotEmpty => !CompareIsEmpty(fieldValue),
                ConditionalLogicConstants.Operators.IsTrue => CompareIsTrue(fieldValue),
                ConditionalLogicConstants.Operators.IsFalse => CompareIsFalse(fieldValue),
                ConditionalLogicConstants.Operators.HasLength => CompareHasLength(fieldValue, condition.Value),
                ConditionalLogicConstants.Operators.MatchesPattern => CompareMatchesPattern(fieldValue, condition.Value),
                ConditionalLogicConstants.Operators.IsValidEmail => CompareIsValidEmail(fieldValue),
                ConditionalLogicConstants.Operators.IsValidPhone => CompareIsValidPhone(fieldValue),
                _ => throw new NotSupportedException($"Operator '{condition.Operator}' is not supported")
            };
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error evaluating condition for field '{FieldId}' with operator '{Operator}'",
                condition.TriggerField, condition.Operator);
            return false;
        }
    }

    public IEnumerable<ConditionalLogic> GetTriggeredRules(IEnumerable<ConditionalLogic> conditionalLogic, string fieldId)
    {
        return conditionalLogic.Where(rule => 
            rule.Enabled && 
            ContainsFieldInConditions(rule.ConditionGroup, fieldId));
    }

    public ConditionalLogicValidationResult ValidateRule(ConditionalLogic rule)
    {
        var result = new ConditionalLogicValidationResult { IsValid = true };

        try
        {
            // Validate rule structure
            if (string.IsNullOrEmpty(rule.Id))
            {
                result.Errors.Add("Rule ID is required");
                result.IsValid = false;
            }

            if (rule.ConditionGroup == null)
            {
                result.Errors.Add("Condition group is required");
                result.IsValid = false;
            }

            if (rule.AffectedElements == null || !rule.AffectedElements.Any())
            {
                result.Errors.Add("At least one affected element is required");
                result.IsValid = false;
            }

            // Validate condition group
            if (rule.ConditionGroup != null)
            {
                ValidateConditionGroup(rule.ConditionGroup, result);
            }

            // Validate affected elements
            if (rule.AffectedElements != null)
            {
                foreach (var element in rule.AffectedElements)
                {
                    ValidateAffectedElement(element, result);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error validating conditional logic rule '{RuleId}'", rule.Id);
            result.Errors.Add($"Validation error: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }

    #region Private Helper Methods

    private bool ContainsFieldInConditions(ConditionGroup conditionGroup, string fieldId)
    {
        if (conditionGroup.Conditions == null) return false;

        return conditionGroup.Conditions.Any(condition =>
            condition.TriggerField == fieldId ||
            (condition.Conditions != null && ContainsFieldInConditions(
                new ConditionGroup { LogicalOperator = "AND", Conditions = condition.Conditions }, fieldId)));
    }

    private void ValidateConditionGroup(ConditionGroup conditionGroup, ConditionalLogicValidationResult result)
    {
        if (conditionGroup.Conditions == null || !conditionGroup.Conditions.Any())
        {
            result.Warnings.Add("Condition group has no conditions");
            return;
        }

        foreach (var condition in conditionGroup.Conditions)
        {
            ValidateCondition(condition, result);
        }
    }

    private void ValidateCondition(Condition condition, ConditionalLogicValidationResult result)
    {
        if (string.IsNullOrEmpty(condition.TriggerField))
        {
            result.Errors.Add("Condition trigger field is required");
            result.IsValid = false;
        }

        if (string.IsNullOrEmpty(condition.Operator))
        {
            result.Errors.Add("Condition operator is required");
            result.IsValid = false;
        }

        // Validate nested conditions
        if (condition.Conditions != null)
        {
            var nestedGroup = new ConditionGroup
            {
                LogicalOperator = condition.LogicalOperator ?? ConditionalLogicConstants.LogicalOperators.And,
                Conditions = condition.Conditions
            };
            ValidateConditionGroup(nestedGroup, result);
        }
    }

    private void ValidateAffectedElement(AffectedElement element, ConditionalLogicValidationResult result)
    {
        if (string.IsNullOrEmpty(element.ElementId))
        {
            result.Errors.Add("Affected element ID is required");
            result.IsValid = false;
        }

        if (string.IsNullOrEmpty(element.ElementType))
        {
            result.Errors.Add("Affected element type is required");
            result.IsValid = false;
        }

        if (string.IsNullOrEmpty(element.Action))
        {
            result.Errors.Add("Affected element action is required");
            result.IsValid = false;
        }
    }

    #region Comparison Methods

    private static bool CompareEquals(object? fieldValue, object expectedValue, string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            ConditionalLogicConstants.DataTypes.Number => CompareNumericEquals(fieldValue, expectedValue),
            ConditionalLogicConstants.DataTypes.Boolean => CompareBooleanEquals(fieldValue, expectedValue),
            ConditionalLogicConstants.DataTypes.Date => CompareDateEquals(fieldValue, expectedValue),
            _ => CompareStringEquals(fieldValue, expectedValue)
        };
    }

    private static bool CompareStringEquals(object? fieldValue, object expectedValue)
    {
        var fieldStr = fieldValue?.ToString() ?? string.Empty;
        var expectedStr = expectedValue?.ToString() ?? string.Empty;
        return string.Equals(fieldStr, expectedStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareNumericEquals(object? fieldValue, object expectedValue)
    {
        if (decimal.TryParse(fieldValue?.ToString(), out var fieldDecimal) &&
            decimal.TryParse(expectedValue?.ToString(), out var expectedDecimal))
        {
            return fieldDecimal == expectedDecimal;
        }
        return false;
    }

    private static bool CompareBooleanEquals(object? fieldValue, object expectedValue)
    {
        if (bool.TryParse(fieldValue?.ToString(), out var fieldBool) &&
            bool.TryParse(expectedValue?.ToString(), out var expectedBool))
        {
            return fieldBool == expectedBool;
        }
        return false;
    }

    private static bool CompareDateEquals(object? fieldValue, object expectedValue)
    {
        if (DateTime.TryParse(fieldValue?.ToString(), out var fieldDate) &&
            DateTime.TryParse(expectedValue?.ToString(), out var expectedDate))
        {
            return fieldDate.Date == expectedDate.Date;
        }
        return false;
    }

    private static bool CompareIn(object? fieldValue, object expectedValue, string dataType)
    {
        var fieldStr = fieldValue?.ToString() ?? string.Empty;

        // Handle array values
        if (expectedValue is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            var values = jsonElement.EnumerateArray().Select(e => e.ToString()).ToList();
            return values.Contains(fieldStr, StringComparer.OrdinalIgnoreCase);
        }

        // Handle string arrays
        if (expectedValue is string[] stringArray)
        {
            return stringArray.Contains(fieldStr, StringComparer.OrdinalIgnoreCase);
        }

        // Handle comma-separated string
        if (expectedValue is string expectedStr)
        {
            var values = expectedStr.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim()).ToList();
            return values.Contains(fieldStr, StringComparer.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool CompareContains(object? fieldValue, object expectedValue)
    {
        var fieldStr = fieldValue?.ToString() ?? string.Empty;
        var expectedStr = expectedValue?.ToString() ?? string.Empty;
        return fieldStr.Contains(expectedStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareStartsWith(object? fieldValue, object expectedValue)
    {
        var fieldStr = fieldValue?.ToString() ?? string.Empty;
        var expectedStr = expectedValue?.ToString() ?? string.Empty;
        return fieldStr.StartsWith(expectedStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareEndsWith(object? fieldValue, object expectedValue)
    {
        var fieldStr = fieldValue?.ToString() ?? string.Empty;
        var expectedStr = expectedValue?.ToString() ?? string.Empty;
        return fieldStr.EndsWith(expectedStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CompareGreaterThan(object? fieldValue, object expectedValue, string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            ConditionalLogicConstants.DataTypes.Number => CompareNumericGreaterThan(fieldValue, expectedValue),
            ConditionalLogicConstants.DataTypes.Date => CompareDateGreaterThan(fieldValue, expectedValue),
            _ => CompareStringGreaterThan(fieldValue, expectedValue)
        };
    }

    private static bool CompareNumericGreaterThan(object? fieldValue, object expectedValue)
    {
        if (decimal.TryParse(fieldValue?.ToString(), out var fieldDecimal) &&
            decimal.TryParse(expectedValue?.ToString(), out var expectedDecimal))
        {
            return fieldDecimal > expectedDecimal;
        }
        return false;
    }

    private static bool CompareDateGreaterThan(object? fieldValue, object expectedValue)
    {
        if (DateTime.TryParse(fieldValue?.ToString(), out var fieldDate) &&
            DateTime.TryParse(expectedValue?.ToString(), out var expectedDate))
        {
            return fieldDate > expectedDate;
        }
        return false;
    }

    private static bool CompareStringGreaterThan(object? fieldValue, object expectedValue)
    {
        var fieldStr = fieldValue?.ToString() ?? string.Empty;
        var expectedStr = expectedValue?.ToString() ?? string.Empty;
        return string.Compare(fieldStr, expectedStr, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static bool CompareLessThan(object? fieldValue, object expectedValue, string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            ConditionalLogicConstants.DataTypes.Number => CompareNumericLessThan(fieldValue, expectedValue),
            ConditionalLogicConstants.DataTypes.Date => CompareDateLessThan(fieldValue, expectedValue),
            _ => CompareStringLessThan(fieldValue, expectedValue)
        };
    }

    private static bool CompareNumericLessThan(object? fieldValue, object expectedValue)
    {
        if (decimal.TryParse(fieldValue?.ToString(), out var fieldDecimal) &&
            decimal.TryParse(expectedValue?.ToString(), out var expectedDecimal))
        {
            return fieldDecimal < expectedDecimal;
        }
        return false;
    }

    private static bool CompareDateLessThan(object? fieldValue, object expectedValue)
    {
        if (DateTime.TryParse(fieldValue?.ToString(), out var fieldDate) &&
            DateTime.TryParse(expectedValue?.ToString(), out var expectedDate))
        {
            return fieldDate < expectedDate;
        }
        return false;
    }

    private static bool CompareStringLessThan(object? fieldValue, object expectedValue)
    {
        var fieldStr = fieldValue?.ToString() ?? string.Empty;
        var expectedStr = expectedValue?.ToString() ?? string.Empty;
        return string.Compare(fieldStr, expectedStr, StringComparison.OrdinalIgnoreCase) < 0;
    }

    private static bool CompareGreaterThanOrEqual(object? fieldValue, object expectedValue, string dataType)
    {
        return CompareGreaterThan(fieldValue, expectedValue, dataType) || 
               CompareEquals(fieldValue, expectedValue, dataType);
    }

    private static bool CompareLessThanOrEqual(object? fieldValue, object expectedValue, string dataType)
    {
        return CompareLessThan(fieldValue, expectedValue, dataType) || 
               CompareEquals(fieldValue, expectedValue, dataType);
    }

    private static bool CompareBetween(object? fieldValue, object expectedValue, string dataType)
    {
        // Expected value should be an array with two elements [min, max]
        if (expectedValue is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            var values = jsonElement.EnumerateArray().ToList();
            if (values.Count == 2)
            {
                var min = values[0];
                var max = values[1];
                return CompareGreaterThanOrEqual(fieldValue, min, dataType) &&
                       CompareLessThanOrEqual(fieldValue, max, dataType);
            }
        }
        return false;
    }

    private static bool CompareIsEmpty(object? fieldValue)
    {
        if (fieldValue == null) return true;
        var fieldStr = fieldValue.ToString();
        return string.IsNullOrWhiteSpace(fieldStr);
    }

    private static bool CompareIsTrue(object? fieldValue)
    {
        if (bool.TryParse(fieldValue?.ToString(), out var boolValue))
        {
            return boolValue;
        }
        var stringValue = fieldValue?.ToString()?.ToLowerInvariant();
        return stringValue is "true" or "yes" or "1" or "on";
    }

    private static bool CompareIsFalse(object? fieldValue)
    {
        return !CompareIsTrue(fieldValue);
    }

    private static bool CompareHasLength(object? fieldValue, object expectedValue)
    {
        var fieldStr = fieldValue?.ToString() ?? string.Empty;
        if (int.TryParse(expectedValue?.ToString(), out var expectedLength))
        {
            return fieldStr.Length == expectedLength;
        }
        return false;
    }

    private static bool CompareMatchesPattern(object? fieldValue, object expectedValue)
    {
        var fieldStr = fieldValue?.ToString() ?? string.Empty;
        var pattern = expectedValue?.ToString() ?? string.Empty;
        
        try
        {
            return Regex.IsMatch(fieldStr, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
        }
        catch
        {
            return false;
        }
    }

    private static bool CompareIsValidEmail(object? fieldValue)
    {
        var fieldStr = fieldValue?.ToString() ?? string.Empty;
        var emailPattern = @"^[^@\s]+@[^@\s]+\.[^@\s]+$";
        return Regex.IsMatch(fieldStr, emailPattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
    }

    private static bool CompareIsValidPhone(object? fieldValue)
    {
        var fieldStr = fieldValue?.ToString() ?? string.Empty;
        // Basic phone validation pattern
        var phonePattern = @"^[\+]?[1-9][\d]{0,15}$";
        var cleanedPhone = Regex.Replace(fieldStr, @"[\s\-\(\)]", "");
        return Regex.IsMatch(cleanedPhone, phonePattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
    }

    #endregion

    #endregion
}
