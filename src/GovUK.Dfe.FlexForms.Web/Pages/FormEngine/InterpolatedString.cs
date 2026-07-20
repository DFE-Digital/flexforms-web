using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace GovUK.Dfe.FlexForms.Web.Pages.FormEngine;

/// <summary>
/// Represents a string that contains placeholders for interpolation with dynamic data.
/// <para>
/// Placeholders have the following syntax:
/// <ul>
/// <li><c>{key}</c> for required placeholders, or</li>
/// <li>
/// <c>{key ?? default_value}</c> for placeholders with a default value if the key is not present in the provided data.
/// </li>
/// </ul>
/// </para>
/// <para>
/// The dynamic data is provided as a <see cref="Dictionary{TKey, TValue}"/> where the keys are the placeholder names,
/// or a resolvable prefix of the property name with a value that can be further indexed into using dot notation.
/// Currently, values that can be further indexed into are <see cref="JsonElement"/> and
/// <see cref="Dictionary{TKey, TValue}"/> objects.
/// </para>
/// <para>
/// Placeholder name resolution is greedy, meaning that if, for example, the key <c>user.firstName</c> is present in the
/// data as a top-level property, it will be preferred over the value of the <c>firstName</c> key of the top-level
/// <c>user</c> item.
/// </para>
/// <para>
/// Placeholders that do not have a default value and that cannot be resolved will be left uninterpolated.
/// </para>
/// </summary>
/// <example>
/// <code>
/// var message = "Hello, { user.firstName ?? World }!";
/// 
/// var data1 = new Dictionary&lt;string, object&gt;
/// {
///     ["user"] = new Dictionary&lt;string, object&gt;
///     {
///         ["firstName"] = "John"
///     }
/// };
/// var result1 = new InterpolatedString(message).Render(data); // "Hello, John!"
/// 
/// var data2 = new Dictionary&lt;string, object&gt;();
/// var result2 = new InterpolatedString(message).Render(data); // "Hello, World!"
/// </code>
/// </example>
public partial record InterpolatedString(string Message)
{
    /// <summary>
    /// Renders the string by interpolating placeholders with values from the provided data.
    /// </summary>
    /// <param name="data">The dynamic data to use to render the string.</param>
    /// <returns>The rendered string.</returns>
    public string Render(Dictionary<string, object> data)
    {
        var chunks = Parse(Message);
        
        var sb = new StringBuilder();
        foreach (var chunk in chunks)
        {
            sb.Append(chunk.Render(data));
        }

        return sb.ToString();
    }

    private static List<InterpolatedStringChunk> Parse(string message)
    {
        var chunks = new List<InterpolatedStringChunk>();
        var index = 0;
        foreach (var match in TagDelimiterRegex().EnumerateMatches(message))
        {
            chunks.Add(new TextChunk(message[index..match.Index]));
            
            var rawText = message.Substring(match.Index, match.Length);
            var tagMatch = TagRegex().Match(rawText);
            var tag = tagMatch.Groups["tag"].Value;
            var defaultValue = tagMatch.Groups["default"].Success ? tagMatch.Groups["default"].Value : null;
            chunks.Add(new TagChunk(rawText, tag, defaultValue));
            
            index = match.Index + match.Length;
        }
        
        chunks.Add(new TextChunk(message[index..]));
        
        return chunks;
    }
    
    // Matches anything between a single balanced pair of braces.
    // This is a simplified version of the regex to enumerate across the message string.
    // `ValueMatch`es don't return information about groups, so matches will have to be individually parsed using
    // `TagRegex`.
    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex TagDelimiterRegex();
    
    // Format should be `{dot.separated.tag}` or `{dot.separated.tag??default value}`.
    // Both the tag and the default value are trimmed.
    [GeneratedRegex(@"^\{\s*(?<tag>[^?}]*?)\s*(?:\?\?\s*(?<default>[^}]*?)\s*)?\}$")]
    private static partial Regex TagRegex();
}

internal abstract record InterpolatedStringChunk(string RawText)
{
    public abstract string Render(Dictionary<string, object> data);
}

internal record TextChunk(string RawText) : InterpolatedStringChunk(RawText)
{
    public override string Render(Dictionary<string, object> data)
    {
        return RawText;
    }
}

internal record TagChunk(string RawText, string Tag, string? DefaultValue) : InterpolatedStringChunk(RawText)
{
    public override string Render(Dictionary<string, object> data)
    {
        var value = Resolve(Tag, data);
        return value?.ToString() ?? DefaultValue ?? RawText;
    }

    private static object? Resolve(string key, Dictionary<string, object> data)
    {
        var prefixCandidates = GetPrefixCandidates(key);

        object? value = null;
        string prefix = null!;
        foreach (var prefixCandidate in prefixCandidates)
        {
            if (data.TryGetValue(prefixCandidate, out value))
            {
                prefix = prefixCandidate;
                break;
            }
        }
        
        if (value is null) return null;
        
        var continueWalk = prefix != key;

        return value switch
        {
            JsonElement jsonElement => continueWalk
                ? jsonElement.EvaluatePath(key[(prefix.Length + 1)..])
                : jsonElement,

            Dictionary<string, object> dictionary => continueWalk
                ? Resolve(key[(prefix.Length + 1)..], dictionary)
                : JsonSerializer.Serialize(dictionary),

            _ => value
        };
    }

    private static string[] GetPrefixCandidates(string key)
    {
        var parts = key.Split('.');
        var result = new string[parts.Length];

        for (var i = 0; i < parts.Length; i++)
        {
            result[i] = string.Join('.', parts.Take(parts.Length - i));
        }

        return result;
    }
}