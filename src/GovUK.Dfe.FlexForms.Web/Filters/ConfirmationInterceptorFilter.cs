using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Web.Filters
{
    /// <summary>
    /// Action filter that intercepts form submissions requiring confirmation
    /// </summary>
    public class ConfirmationInterceptorFilter : IActionFilter, IAsyncPageFilter, IOrderedFilter
    {
        private readonly IButtonConfirmationService _confirmationService;
        private readonly ILogger<ConfirmationInterceptorFilter> _logger;

        public ConfirmationInterceptorFilter(
            IButtonConfirmationService confirmationService,
            ILogger<ConfirmationInterceptorFilter> logger)
        {
            _confirmationService = confirmationService;
            _logger = logger;
        }

        /// <summary>
        /// Execute early in the pipeline to intercept before page handlers
        /// </summary>
        public int Order => -1000;

        /// <summary>
        /// Called before the action method is executed
        /// </summary>
        /// <param name="context">The action executing context</param>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            // Only intercept POST requests to page handlers
            if (context.HttpContext.Request.Method != "POST")
                return;

            if (context.ActionDescriptor.RouteValues.TryGetValue("controller", out var controller))
            {
                _logger.LogDebug("Skipping confirmation interception for API controller: {Controller}", controller);
                return;
            }

            // Skip if this is already a confirmed action coming back from confirmation page
            if (context.HttpContext.Request.Query.ContainsKey("confirmed") &&
                context.HttpContext.Request.Query["confirmed"] == "true")
            {
                _logger.LogInformation("Skipping confirmation interception - this is a confirmed action");
                return;
            }

            var request = context.HttpContext.Request;

            if (request.ContentType == null)
                return;

            if (request?.Form == null)
                return;

            var form = request.Form;

            // Check if any button in the form requires confirmation
            var confirmationInfo = FindConfirmationButton(form);
            if (confirmationInfo == null)
                return;

            _logger.LogInformation("Intercepting form submission for confirmation - Handler: {Handler}, DisplayFields: {DisplayFields}",
                confirmationInfo.Handler, string.Join(",", confirmationInfo.DisplayFields));

            // Create confirmation request
            var (title, requiredMessage) = ReadCustomMeta(form, confirmationInfo.Handler);

            // Allow overriding the action path via hidden meta field
            var overrideActionKey = $"confirmation-action-{confirmationInfo.Handler}";
            var originalPath = context.HttpContext.Request.Path;
            if (form.ContainsKey(overrideActionKey))
            {
                var values = form[overrideActionKey];
                var desired = values.Count > 0 ? values[0] : string.Empty;
                if (!string.IsNullOrWhiteSpace(desired))
                {
                    // Guard against accidental CSV joining from multiple inputs
                    originalPath = desired.Split(',')[0].Trim();
                }
            }

            // Allow overriding the return URL (where to go on cancel/back)
            var overrideReturnKey = $"confirmation-return-{confirmationInfo.Handler}";
            var returnUrl = $"{context.HttpContext.Request.Path}{context.HttpContext.Request.QueryString}";
            if (form.ContainsKey(overrideReturnKey))
            {
                var values = form[overrideReturnKey];
                var desiredReturn = values.Count > 0 ? values[0] : string.Empty;
                if (!string.IsNullOrWhiteSpace(desiredReturn))
                {
                    returnUrl = desiredReturn.Split(',')[0].Trim();
                }
            }

            var confirmationRequest = new ConfirmationRequest
            {
                OriginalPagePath = originalPath,
                OriginalHandler = confirmationInfo.Handler,
                OriginalFormData = ExtractFormData(form),
                DisplayFields = confirmationInfo.DisplayFields,
                ReturnUrl = returnUrl,
                Title = title,
                RequiredMessage = requiredMessage,
            };

            // Store confirmation context and redirect
            try
            {
                var token = _confirmationService.CreateConfirmation(confirmationRequest);
                context.Result = new RedirectToPageResult("/Confirmation/Index", new { token });

                _logger.LogInformation("Redirecting to confirmation page with token {Token}", token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create confirmation for handler {Handler}", confirmationInfo.Handler);
                // Let the original action proceed if confirmation creation fails
            }
        }

        /// <summary>
        /// Called after the action method is executed (not used)
        /// </summary>
        /// <param name="context">The action executed context</param>
        public void OnActionExecuted(ActionExecutedContext context)
        {
            // Not used for this filter
        }

        // Razor Pages pipeline interception
        public System.Threading.Tasks.Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
        {
            // Not used
            return System.Threading.Tasks.Task.CompletedTask;
        }

        public async System.Threading.Tasks.Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            // Only intercept POST requests to page handlers
            if (!HttpMethods.IsPost(context.HttpContext.Request.Method))
            {
                await next();
                return;
            }

            // Skip if this is already a confirmed action coming back from confirmation page
            if (context.HttpContext.Request.Query.ContainsKey("confirmed") &&
                context.HttpContext.Request.Query["confirmed"] == "true")
            {
                _logger.LogInformation("Skipping confirmation interception (Razor Pages) - already confirmed");
                await next();
                return;
            }

            var request = context.HttpContext.Request;
            var form = request.HasFormContentType ? await request.ReadFormAsync() : default;

            if (form.Count == 0)
            {
                await next();
                return;
            }

            var confirmationInfo = FindConfirmationButton(form);
            if (confirmationInfo == null)
            {
                await next();
                return;
            }

            _logger.LogInformation("[Pages] Found confirmation button - Handler: {Handler}, DisplayFields: {DisplayFields}. Executing handler first for validation.",
                confirmationInfo.Handler, string.Join(",", confirmationInfo.DisplayFields));
            
            // Execute the page handler first to allow validation to run
            var executedContext = await next();
            
            // After handler execution, check if ModelState is valid
            // If validation failed, the handler will have returned Page() with errors, so don't intercept
            if (executedContext.ModelState != null && !executedContext.ModelState.IsValid)
            {
                _logger.LogInformation("[Pages] ModelState is invalid after handler execution. Skipping confirmation interception to show validation errors.");
                return;
            }
            
            // If validation passed, intercept the result and redirect to confirmation instead
            _logger.LogInformation("[Pages] Validation passed. Intercepting result ({ResultType}) for confirmation - Handler: {Handler}",
                executedContext.Result?.GetType().Name ?? "null", confirmationInfo.Handler);

            var (title2, requiredMessage2) = ReadCustomMeta(form, confirmationInfo.Handler);

            // Allow overriding the action path via hidden meta field
            var overrideActionKey2 = $"confirmation-action-{confirmationInfo.Handler}";
            var originalPath2 = context.HttpContext.Request.Path;
            if (form.ContainsKey(overrideActionKey2))
            {
                var values = form[overrideActionKey2];
                var desired = values.Count > 0 ? values[0] : string.Empty;
                if (!string.IsNullOrWhiteSpace(desired))
                {
                    originalPath2 = desired.Split(',')[0].Trim();
                }
            }

            // Allow overriding the return URL (where to go on cancel/back)
            var overrideReturnKey2 = $"confirmation-return-{confirmationInfo.Handler}";
            var returnUrl2 = $"{context.HttpContext.Request.Path}{context.HttpContext.Request.QueryString}";
            if (form.ContainsKey(overrideReturnKey2))
            {
                var values = form[overrideReturnKey2];
                var desiredReturn2 = values.Count > 0 ? values[0] : string.Empty;
                if (!string.IsNullOrWhiteSpace(desiredReturn2))
                {
                    returnUrl2 = desiredReturn2.Split(',')[0].Trim();
                }
            }

            var confirmationRequest = new ConfirmationRequest
            {
                OriginalPagePath = originalPath2,
                OriginalHandler = confirmationInfo.Handler,
                OriginalFormData = ExtractFormData(form),
                DisplayFields = confirmationInfo.DisplayFields,
                ReturnUrl = returnUrl2,
                Title = title2,
                RequiredMessage = requiredMessage2,
            };

            try
            {
                var token = _confirmationService.CreateConfirmation(confirmationRequest);
                executedContext.Result = new RedirectToPageResult("/Confirmation/Index", new { token });
                _logger.LogInformation("[Pages] Redirecting to confirmation page with token {Token}", token);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Pages] Failed to create confirmation for handler {Handler}", confirmationInfo.Handler);
                // If confirmation creation fails, let the original handler result proceed
            }
        }

        /// <summary>
        /// Finds confirmation button information in the form data
        /// </summary>
        /// <param name="form">The form collection</param>
        /// <returns>Confirmation button information or null if none found</returns>
        private ConfirmationButtonInfo? FindConfirmationButton(IFormCollection form)
        {
            // Look for the handler that was clicked
            string? clickedHandler = null;

            // Check for handler field (standard Razor Pages pattern)
            if (form.ContainsKey("handler") && form["handler"].Count > 0)
            {
                clickedHandler = form["handler"].ToString();
            }

            if (string.IsNullOrEmpty(clickedHandler))
                return null;

            // Check if this handler requires confirmation
            var confirmationCheckKey = $"confirmation-check-{clickedHandler}";
            var displayFieldsKey = $"confirmation-display-fields-{clickedHandler}";

            if (form.ContainsKey(confirmationCheckKey) && form[confirmationCheckKey] == "true")
            {
                var displayFieldsValue = form.ContainsKey(displayFieldsKey)
                    ? form[displayFieldsKey].ToString()
                    : string.Empty;

                var displayFields = string.IsNullOrEmpty(displayFieldsValue)
                    ? Array.Empty<string>()
                    : displayFieldsValue.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(f => f.Trim())
                        .ToArray();

                return new ConfirmationButtonInfo
                {
                    Handler = clickedHandler,
                    DisplayFields = displayFields
                };
            }

            return null;
        }

        /// <summary>
        /// Extracts form data into a dictionary
        /// </summary>
        /// <param name="form">The form collection</param>
        /// <returns>Dictionary of form data</returns>
        private Dictionary<string, object> ExtractFormData(IFormCollection form)
        {
            var formData = new Dictionary<string, object>();

            foreach (var key in form.Keys)
            {
                // Skip confirmation-related hidden fields
                if (key.StartsWith("confirmation-"))
                    continue;

                var values = form[key];
                if (values.Count == 1)
                {
                    formData[key] = values[0] ?? string.Empty;
                }
                else if (values.Count > 1)
                {
                    formData[key] = values.ToArray();
                }
                else
                {
                    formData[key] = string.Empty;
                }
            }

            _logger.LogDebug("Extracted {Count} form fields for confirmation", formData.Count);
            return formData;
        }

        private static (string? Title, string? RequiredMessage) ReadCustomMeta(IFormCollection form, string handler)
        {
            try
            {
                var titleKey = $"confirmation-title-{handler}";
                var messageKey = $"confirmation-taskName-{handler}";
                var requiredMessageKey = $"confirmation-requiredMessage-{handler}";
                string? title = form.ContainsKey(titleKey) ? form[titleKey].ToString() : null;
                string? message = form.ContainsKey(messageKey) ? form[messageKey].ToString() : null;
                string? requiredMessage = form.ContainsKey(requiredMessageKey) ? form[requiredMessageKey].ToString() : null;
                title = string.IsNullOrWhiteSpace(title) ? null : title;
                message = string.IsNullOrWhiteSpace(message) ? null : message;
                requiredMessage = string.IsNullOrWhiteSpace(requiredMessage) ? null : requiredMessage;
                return (title, requiredMessage);
            }
            catch
            {
                return (null, null);
            }
        }
    }

    /// <summary>
    /// Information about a button that requires confirmation
    /// </summary>
    public class ConfirmationButtonInfo
    {
        /// <summary>
        /// The handler name for the button
        /// </summary>
        public string Handler { get; set; } = string.Empty;

        /// <summary>
        /// The fields to display on the confirmation page
        /// </summary>
        public string[] DisplayFields { get; set; } = Array.Empty<string>();
    }
}
