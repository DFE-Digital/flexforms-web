using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GovUK.Dfe.FlexForms.Domain.Models;

/// <summary>
/// Evaluates autocomplete display expressions such as
/// <c>displayName + " - " + constituencyName</c> or <c>{firstName} {lastName}</c>.
/// </summary>
public static class AutocompleteDisplayExpression
{
    private static readonly Regex PlaceholderPattern = new(
        @"\{([A-Za-z_][A-Za-z0-9_]*)\}",
        RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public static bool IsSpecified(string? expression) =>
        !string.IsNullOrWhiteSpace(expression);

    public static string FirstNonEmpty(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred.Trim() : fallback?.Trim() ?? string.Empty;

    public static string Evaluate(string? expression, IReadOnlyDictionary<string, object>? values)
    {
        if (!IsSpecified(expression))
            return string.Empty;

        var lookup = values is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : values.ToDictionary(
                kv => kv.Key,
                kv => kv.Value?.ToString() ?? string.Empty,
                StringComparer.OrdinalIgnoreCase);

        return EvaluateCore(expression!, lookup);
    }

    public static string Evaluate(string? expression, JsonElement element)
    {
        if (!IsSpecified(expression) || element.ValueKind != JsonValueKind.Object)
            return string.Empty;

        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            var value = ReadScalar(property.Value);
            if (value != null)
                lookup[property.Name] = value;
        }

        return EvaluateCore(expression!, lookup);
    }

    public static IReadOnlyList<string> GetPropertyNames(string? expression)
    {
        if (!IsSpecified(expression))
            return [];

        return Parse(expression!)
            .Where(token => !token.IsLiteral)
            .Select(token => token.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string EvaluateCore(string expression, IReadOnlyDictionary<string, string> values)
    {
        var tokens = Parse(expression);
        if (tokens.Count == 0)
            return string.Empty;

        var resolved = tokens
            .Select(token =>
            {
                if (token.IsLiteral)
                    return token.Value;

                values.TryGetValue(token.Value, out var propertyValue);
                return propertyValue ?? string.Empty;
            })
            .ToList();

        var builder = new StringBuilder();
        for (var i = 0; i < tokens.Count; i++)
        {
            if (!tokens[i].IsLiteral && string.IsNullOrWhiteSpace(resolved[i]))
                continue;

            if (tokens[i].IsLiteral && IsSeparatorLiteral(tokens[i].Value))
            {
                if (builder.Length == 0 || !HasNonEmptyPropertyAfter(tokens, resolved, i))
                    continue;
            }

            builder.Append(resolved[i]);
        }

        return builder.ToString().Trim();
    }

    private static bool IsSeparatorLiteral(string value) =>
        string.IsNullOrWhiteSpace(value)
        || value.All(ch => char.IsWhiteSpace(ch) || ch is '-' or '|' or '/' or ',' or ':');

    private static bool HasNonEmptyPropertyAfter(
        IReadOnlyList<Token> tokens,
        IReadOnlyList<string> resolved,
        int index)
    {
        for (var i = index + 1; i < tokens.Count; i++)
        {
            if (tokens[i].IsLiteral)
                continue;

            if (!string.IsNullOrWhiteSpace(resolved[i]))
                return true;
        }

        return false;
    }

    private static List<Token> Parse(string expression)
    {
        var trimmed = expression.Trim();
        if (trimmed.Contains('{') && PlaceholderPattern.IsMatch(trimmed) && !trimmed.Contains('+'))
            return ParsePlaceholders(trimmed);

        return ParseConcatenation(trimmed);
    }

    private static List<Token> ParsePlaceholders(string expression)
    {
        var tokens = new List<Token>();
        var lastIndex = 0;
        foreach (Match match in PlaceholderPattern.Matches(expression))
        {
            if (match.Index > lastIndex)
                tokens.Add(Token.Literal(expression[lastIndex..match.Index]));

            tokens.Add(Token.Property(match.Groups[1].Value));
            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < expression.Length)
            tokens.Add(Token.Literal(expression[lastIndex..]));

        return tokens;
    }

    private static List<Token> ParseConcatenation(string expression)
    {
        var tokens = new List<Token>();
        var i = 0;
        while (i < expression.Length)
        {
            while (i < expression.Length && char.IsWhiteSpace(expression[i]))
                i++;
            if (i >= expression.Length)
                break;

            if (expression[i] == '"')
            {
                var (literal, next) = ReadQuoted(expression, i, '"');
                tokens.Add(Token.Literal(literal));
                i = next;
            }
            else if (expression[i] == '\'')
            {
                var (literal, next) = ReadQuoted(expression, i, '\'');
                tokens.Add(Token.Literal(literal));
                i = next;
            }
            else if (expression[i] == '+')
            {
                i++;
            }
            else
            {
                var start = i;
                while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
                    i++;

                if (i == start)
                {
                    i++;
                    continue;
                }

                tokens.Add(Token.Property(expression[start..i]));
            }
        }

        return tokens;
    }

    private static (string Value, int Next) ReadQuoted(string expression, int start, char quote)
    {
        var i = start + 1;
        var builder = new StringBuilder();
        while (i < expression.Length)
        {
            if (expression[i] == quote)
                return (builder.ToString(), i + 1);

            builder.Append(expression[i]);
            i++;
        }

        return (builder.ToString(), i);
    }

    private static string? ReadScalar(JsonElement value) =>
        value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.ToString(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };

    private readonly record struct Token(bool IsLiteral, string Value)
    {
        public static Token Literal(string value) => new(true, value);
        public static Token Property(string name) => new(false, name);
    }
}
