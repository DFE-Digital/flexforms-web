using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Web.Pages.Confirmation
{
    /// <summary>
    /// Page model for the confirmation page
    /// </summary>
    public class IndexModel : PageModel
    {
        private readonly IButtonConfirmationService _confirmationService;
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(
            IButtonConfirmationService confirmationService,
            ILogger<IndexModel> logger)
        {
            _confirmationService = confirmationService;
            _logger = logger;
        }

        /// <summary>
        /// Whether the user has confirmed the action
        /// </summary>
        [BindProperty]
        public bool Confirmed { get; set; }

        /// <summary>
        /// The confirmation token
        /// </summary>
        [BindProperty]
        public string ConfirmationToken { get; set; } = string.Empty;

        /// <summary>
        /// The display model for the confirmation page
        /// </summary>
        public ConfirmationDisplayModel DisplayModel { get; set; } = new();

        /// <summary>
        /// Handles GET requests to display the confirmation page
        /// </summary>
        /// <param name="token">The confirmation token</param>
        /// <returns>The page result or redirect if invalid</returns>
        public IActionResult OnGet(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogWarning("Confirmation page accessed without token");
                return RedirectToPage("/Error/General");
            }

            DisplayModel = _confirmationService.PrepareDisplayModel(token);
            if (DisplayModel == null)
            {
                _logger.LogWarning("Invalid or expired confirmation token: {Token}", token);
                return RedirectToPage("/Error/General");
            }

            ConfirmationToken = token;
            _logger.LogInformation("Displaying confirmation page for token {Token}", token);
            
            return Page();
        }

        /// <summary>
        /// Handles POST: reads the user's choice and either executes the original action or returns
        /// </summary>
        public IActionResult OnPost()
        {
            var token = ConfirmationToken;
            var context = _confirmationService.GetConfirmation(token);
            if (context == null)
            {
                _logger.LogWarning("Invalid or expired confirmation token on POST: {Token}", token);
                return RedirectToPage("/Error/General");
            }

            // Determine outcome from radio - require explicit selection
            var confirmedValue = Request.Form["Confirmed"].ToString();
            if (string.IsNullOrEmpty(confirmedValue))
            {
                // Rebuild display model and show server-side validation error
                DisplayModel = _confirmationService.PrepareDisplayModel(token) ?? new ConfirmationDisplayModel();
                ConfirmationToken = token;

                ModelState.AddModelError("Confirmed", DisplayModel.RequiredMessage!);
                return Page();
            }

            var isConfirmed = string.Equals(confirmedValue, "true", StringComparison.OrdinalIgnoreCase);

            if (!isConfirmed)
            {
                var back = context.Request.ReturnUrl ?? "/";
                if (Uri.TryCreate(back, UriKind.Absolute, out var abs))
                {
                    back = abs.PathAndQuery;
                }

                _confirmationService.ClearConfirmation(token);
                _logger.LogInformation("User cancelled confirmation; local redirect to {ReturnUrl}", back);
                return LocalRedirect(back);
            }

            return ExecuteOriginalAction(context);
        }

        /// <summary>
        /// Executes the original action that was intercepted
        /// </summary>
        /// <param name="context">The confirmation context</param>
        /// <returns>Redirect to execute the original action</returns>
        private IActionResult ExecuteOriginalAction(ConfirmationContext context)
        {
            try
            {
                var originalPath = context.Request.OriginalPagePath;
                var originalHandler = context.Request.OriginalHandler;
                var formData = context.Request.OriginalFormData;

                // Store the confirmed form data in TempData so the target page can access it if needed
                TempData["ConfirmedFormData"] = JsonSerializer.Serialize(formData);
                TempData["ConfirmedHandler"] = originalHandler;

                // Clear the confirmation as it's been used
                _confirmationService.ClearConfirmation(ConfirmationToken);

                // Preserve the POST and body so the original handler executes as intended
                var redirectUrl = $"{originalPath}?confirmed=true&handler={originalHandler}";
                _logger.LogInformation("Redirecting (preserve method) to execute original action: {RedirectUrl}", redirectUrl);
                return new RedirectResult(redirectUrl, permanent: false, preserveMethod: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute original action for confirmation token {Token}", ConfirmationToken);
                return RedirectToPage("/Error/General");
            }
        }
    }
}
