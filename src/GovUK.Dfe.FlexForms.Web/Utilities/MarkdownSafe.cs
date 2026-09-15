using AngleSharp.Dom;
using Ganss.Xss;
using Markdig;
using System;
using System.Text.RegularExpressions;

namespace GovUK.Dfe.FlexForms.Web.Utilities
{
    /// <summary>
    /// Safe Markdown renderer for JSON text fields (tooltips/help).
    /// - Disables raw HTML input
    /// - Sanitises output to a tight allow-list
    /// - Forces links to open safely in a new tab
    /// - Allows only https by default (toggleable)
    /// - Treats single '\n' as <br>
    /// </summary>
    public static class MarkdownSafe
    {
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .DisableHtml()
            .UseSoftlineBreakAsHardlineBreak()
            .Build();

        private static readonly HtmlSanitizer SanitizerHttpsOnly = CreateSanitizer(allowHttp: false);
        private static readonly HtmlSanitizer SanitizerHttpAndHttps = CreateSanitizer(allowHttp: true);
        private static readonly HtmlSanitizer SanitizerGovUkContent = CreateSanitizer(
            allowHttp: false,
            allowMailto: true,
            allowHeadings: true);

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

        // Strip anchors without href
        private static readonly Regex AnchorWithoutHref =
            new(@"<a(?![^>]*\bhref=)[^>]*>(.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, RegexTimeout);

        // Empty paragraphs
        private static readonly Regex EmptyParagraph =
            new(@"<p>\s*</p>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, RegexTimeout);

        // Exactly one <p>...</p>
        private static readonly Regex SingleParagraph =
            new(@"^\s*<p>([\s\S]*)<\/p>\s*$", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, RegexTimeout);

        // Count paragraph tags
        private static readonly Regex ParagraphTag =
            new(@"<p\b[^>]*>", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, RegexTimeout);

        // Presence of a list block
        private static readonly Regex HasListBlock =
            new(@"<\s*(ul|ol)\b", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled, RegexTimeout);

        private static readonly Regex ParagraphOpen =
            new(@"<p\b([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

        private static readonly Regex UnorderedListOpen =
            new(@"<ul\b([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

        private static readonly Regex OrderedListOpen =
            new(@"<ol\b([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

        private static readonly Regex Heading1Open =
            new(@"<h1\b([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

        private static readonly Regex Heading2Open =
            new(@"<h2\b([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

        private static readonly Regex Heading3Open =
            new(@"<h3\b([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

        private static readonly Regex AnchorOpen =
            new(@"<a\b([^>]*)>", RegexOptions.IgnoreCase | RegexOptions.Compiled, RegexTimeout);

        private static HtmlSanitizer CreateSanitizer(
            bool allowHttp,
            bool allowMailto = false,
            bool allowHeadings = false)
        {
            var s = new HtmlSanitizer();

            // Allowed tags
            s.AllowedTags.Clear();
            s.AllowedTags.Add("p");
            s.AllowedTags.Add("br");
            s.AllowedTags.Add("strong");
            s.AllowedTags.Add("em");
            s.AllowedTags.Add("ul");
            s.AllowedTags.Add("ol");
            s.AllowedTags.Add("li");
            s.AllowedTags.Add("a");
            if (allowHeadings)
            {
                s.AllowedTags.Add("h1");
                s.AllowedTags.Add("h2");
                s.AllowedTags.Add("h3");
            }

            // Allowed attributes
            s.AllowedAttributes.Clear();
            s.AllowedAttributes.Add("href");
            s.AllowedAttributes.Add("target");
            s.AllowedAttributes.Add("rel");
            if (allowHeadings)
                s.AllowedAttributes.Add("class");

            // Allowed schemes
            s.AllowedSchemes.Clear();
            s.AllowedSchemes.Add("https");
            if (allowHttp) s.AllowedSchemes.Add("http");
            if (allowMailto) s.AllowedSchemes.Add("mailto");

            // Normalise anchors after sanitising
            s.PostProcessNode += (_, e) =>
            {
                if (e.Node is IElement el && el.TagName.Equals("A", StringComparison.OrdinalIgnoreCase))
                {
                    var href = el.GetAttribute("href");
                    if (string.IsNullOrWhiteSpace(href))
                    {
                        return;
                    }

                    // mailto: must stay in the same tab so the OS hands off to the mail client
                    // instead of leaving an empty browser tab behind.
                    if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase))
                    {
                        el.RemoveAttribute("target");
                        el.RemoveAttribute("rel");
                        return;
                    }

                    el.SetAttribute("target", "_blank");
                    el.SetAttribute("rel", "noopener noreferrer");
                }
            };

            return s;
        }

        /// <summary>
        /// Normalise whitespace: turn space-only lines into real blank lines; trim trailing spaces.
        /// </summary>
        private static string NormaliseWhitespace(string s)
        {
            s = s.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = s.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) lines[i] = string.Empty;
                else lines[i] = lines[i].TrimEnd();
            }
            return string.Join("\n", lines);
        }

        /// <summary>
        /// Convert Markdown to sanitised HTML. Returns empty string for null/whitespace input.
        /// </summary>
        public static string ToSafeHtml(string? markdown, int maxChars = 8000, bool allowHttp = false)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;

            if (markdown.Length > maxChars)
                markdown = markdown.Substring(0, maxChars);

            markdown = NormaliseWhitespace(markdown);

            var rawHtml = Markdig.Markdown.ToHtml(markdown, Pipeline);

            var sanitizer = allowHttp ? SanitizerHttpAndHttps : SanitizerHttpsOnly;
            var safe = sanitizer.Sanitize(rawHtml);

            // Clean up artefacts
            safe = AnchorWithoutHref.Replace(safe, "$1");
            safe = EmptyParagraph.Replace(safe, string.Empty);

            return safe;
        }

        /// <summary>
        /// Convert Markdown to sanitised HTML with GOV.UK content classes.
        /// Allows headings and mailto links for admin-authored confirmation copy.
        /// </summary>
        public static string ToSafeGovUkHtml(string? markdown, int maxChars = 20000)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                return string.Empty;

            if (markdown.Length > maxChars)
                markdown = markdown.Substring(0, maxChars);

            markdown = NormaliseWhitespace(markdown);

            var rawHtml = Markdig.Markdown.ToHtml(markdown, Pipeline);
            var safe = SanitizerGovUkContent.Sanitize(rawHtml);

            safe = AnchorWithoutHref.Replace(safe, "$1");
            safe = EmptyParagraph.Replace(safe, string.Empty);

            return ApplyGovUkContentClasses(safe);
        }

        private static string ApplyGovUkContentClasses(string html)
        {
            html = ParagraphOpen.Replace(html, "<p class=\"govuk-body\"$1>");
            html = UnorderedListOpen.Replace(html, "<ul class=\"govuk-list govuk-list--bullet\"$1>");
            html = OrderedListOpen.Replace(html, "<ol class=\"govuk-list govuk-list--number\"$1>");
            html = Heading1Open.Replace(html, "<h1 class=\"govuk-heading-l\"$1>");
            html = Heading2Open.Replace(html, "<h2 class=\"govuk-heading-m\"$1>");
            html = Heading3Open.Replace(html, "<h3 class=\"govuk-heading-s\"$1>");
            html = AnchorOpen.Replace(html, MatchGovUkLink);
            return html;
        }

        private static string MatchGovUkLink(Match match)
        {
            var attrs = match.Groups[1].Value;
            return attrs.Contains("govuk-link", StringComparison.OrdinalIgnoreCase)
                ? match.Value
                : $"<a class=\"govuk-link\"{attrs}>";
        }

        /// <summary>
        /// Convert Markdown to sanitised HTML and unwrap a single &lt;p&gt;…&lt;/p&gt;.
        /// Useful for hint text where a block wrapper is undesirable.
        /// </summary>
        public static string ToSafeHtmlInline(string? markdown, int maxChars = 8000, bool allowHttp = false)
        {
            var safe = ToSafeHtml(markdown, maxChars, allowHttp);
            var m = SingleParagraph.Match(safe);
            return m.Success ? m.Groups[1].Value : safe;
        }

        /// <summary>
        /// Returns true if the provided HTML consists of exactly one &lt;p&gt;…&lt;/p&gt; block.
        /// </summary>
        public static bool IsSingleParagraphFromHtml(string? html)
        {
            if (string.IsNullOrWhiteSpace(html)) return false;
            return SingleParagraph.IsMatch(html.Trim());
        }

        /// <summary>
        /// Render once for hint usage and decide class automatically:
        /// - Single paragraph → unwrap &lt;p&gt;…&lt;/p&gt;, no class (keep default grey).
        /// - Multi (2+ paragraphs or any list) → keep blocks, return "hint--default" (black/default text colour).
        /// </summary>
        public static (string html, string? cssClass) RenderHintWithClass(string? markdown, int maxChars = 8000, bool allowHttp = false)
        {
            var full = ToSafeHtml(markdown, maxChars, allowHttp); // already normalised & cleaned
            var paraCount = ParagraphTag.Matches(full).Count;
            var hasList = HasListBlock.IsMatch(full);
            var isMulti = paraCount >= 2 || hasList;

            var html = isMulti ? full : ToSafeHtmlInline(markdown, maxChars, allowHttp);
            var cssClass = isMulti ? "hint--default" : null;
            return (html, cssClass);
        }
    }
}
