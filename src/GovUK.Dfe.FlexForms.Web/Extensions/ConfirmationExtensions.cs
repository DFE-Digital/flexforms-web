using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Text;

namespace GovUK.Dfe.FlexForms.Web.Extensions
{
    /// <summary>
    /// Extension methods for rendering confirmation buttons
    /// </summary>
    public static class ConfirmationExtensions
    {
        /// <summary>
        /// Renders a button with optional confirmation functionality
        /// </summary>
        public static IHtmlContent RenderConfirmationButton(
            this IHtmlHelper htmlHelper,
            string buttonText,
            string handler = "Page",
            string buttonClass = "govuk-button",
            bool requiresConfirmation = false,
            string displayFields = "",
            string buttonType = "submit",
            string? buttonId = null,
            object? additionalAttributes = null,
            string? title = null,
            string? requiredMessage = null)
        {
            var html = new StringBuilder();

            // Start button element
            html.Append($"<button type=\"{buttonType}\" name=\"handler\" value=\"{handler}\" class=\"{buttonClass}\"");

            // Add ID if provided
            if (!string.IsNullOrEmpty(buttonId))
            {
                html.Append($" id=\"{buttonId}\"");
            }

            // Add any additional attributes (e.g. style)
            if (additionalAttributes != null)
            {
                var attributes = HtmlHelper.AnonymousObjectToHtmlAttributes(additionalAttributes);
                foreach (var attribute in attributes)
                {
                    var attributeName = attribute.Key;
                    var attributeValue = System.Net.WebUtility.HtmlEncode(attribute.Value?.ToString());
                    html.Append($" {attributeName}=\"{attributeValue}\"");
                }
            }

            // Additional attributes placeholder
            html.Append(">");
            html.Append(buttonText);
            html.AppendLine("</button>");

            if (requiresConfirmation)
            {
                html.AppendLine($"<input type=\"hidden\" name=\"confirmation-check-{handler}\" value=\"true\" />");
                if (!string.IsNullOrEmpty(displayFields))
                {
                    html.AppendLine($"<input type=\"hidden\" name=\"confirmation-display-fields-{handler}\" value=\"{displayFields}\" />");
                }
                if (!string.IsNullOrWhiteSpace(title))
                {
                    html.AppendLine($"<input type=\"hidden\" name=\"confirmation-title-{handler}\" value=\"{System.Net.WebUtility.HtmlEncode(title)}\" />");
                }
                if (!string.IsNullOrWhiteSpace(requiredMessage))
                {
                    html.AppendLine($"<input type=\"hidden\" name=\"confirmation-requiredMessage-{handler}\" value=\"{System.Net.WebUtility.HtmlEncode(requiredMessage)}\" />");
                }
                // message removed
            }

            return new HtmlString(html.ToString());
        }

        public static IHtmlContent RenderPrimaryConfirmationButton(
            this IHtmlHelper htmlHelper,
            string buttonText,
            string handler = "Page",
            string displayFields = "",
            string? buttonId = null,
            string? title = null,
            string? requiredMessage = null)
        {
            return htmlHelper.RenderConfirmationButton(
                buttonText: buttonText,
                handler: handler,
                buttonClass: "govuk-button",
                requiresConfirmation: true,
                displayFields: displayFields,
                buttonId: buttonId,
                title: title,
                requiredMessage: requiredMessage);
        }

        public static IHtmlContent RenderSecondaryConfirmationButton(
            this IHtmlHelper htmlHelper,
            string buttonText,
            string handler = "Page",
            string displayFields = "",
            string? buttonId = null,
            string? title = null,
            string? requiredMessage = null)
        {
            return htmlHelper.RenderConfirmationButton(
                buttonText: buttonText,
                handler: handler,
                buttonClass: "govuk-button govuk-button--secondary",
                requiresConfirmation: true,
                displayFields: displayFields,
                buttonId: buttonId,
                title: title,
                requiredMessage: requiredMessage);
        }

        public static IHtmlContent RenderWarningConfirmationButton(
            this IHtmlHelper htmlHelper,
            string buttonText,
            string handler = "Page",
            string displayFields = "",
            string? buttonId = null,
            string? title = null,
            string? requiredMessage = null)
        {
            return htmlHelper.RenderConfirmationButton(
                buttonText: buttonText,
                handler: handler,
                buttonClass: "govuk-button govuk-button--warning",
                requiresConfirmation: true,
                displayFields: displayFields,
                buttonId: buttonId,
                title: title,
                requiredMessage: requiredMessage);
        }

        public static IHtmlContent RenderLinkConfirmationButton(
            this IHtmlHelper htmlHelper,
            string buttonText,
            string handler = "Page",
            string displayFields = "",
            string? buttonId = null,
            string? title = null,
            string? requiredMessage = null)
        {
            return htmlHelper.RenderConfirmationButton(
                buttonText: buttonText,
                handler: handler,
                buttonClass: "govuk-link",
                requiresConfirmation: true,
                displayFields: displayFields,
                buttonType: "submit",
                buttonId: buttonId,
                additionalAttributes: new
                {
                    style = "background: none; border: 0; padding: 0; font: inherit; cursor: pointer; font-family: GDS Transport, arial, sans-serif; -webkit-font-smoothing: antialiased; -moz-osx-font-smoothing: grayscale; text-decoration: underline; text-decoration-thickness: max(1px, .0625rem); text-underline-offset: .1578em; color: #1d70b8;"
                },
                title: title,
                requiredMessage: requiredMessage);
        }
    }
}

