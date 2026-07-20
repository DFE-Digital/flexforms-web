using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Application.Interfaces
{
    /// <summary>
    /// Service for handling button confirmation functionality
    /// </summary>
    public interface IButtonConfirmationService
    {
        /// <summary>
        /// Creates a confirmation request and returns a token
        /// </summary>
        /// <param name="request">The confirmation request details</param>
        /// <returns>A unique confirmation token</returns>
        string CreateConfirmation(ConfirmationRequest request);

        /// <summary>
        /// Retrieves a confirmation context by token
        /// </summary>
        /// <param name="token">The confirmation token</param>
        /// <returns>The confirmation context or null if not found/expired</returns>
        ConfirmationContext? GetConfirmation(string token);

        /// <summary>
        /// Prepares the display model for the confirmation page
        /// </summary>
        /// <param name="token">The confirmation token</param>
        /// <returns>The display model or null if token is invalid</returns>
        ConfirmationDisplayModel? PrepareDisplayModel(string token);

        /// <summary>
        /// Clears an expired or used confirmation from storage
        /// </summary>
        /// <param name="token">The confirmation token to clear</param>
        void ClearConfirmation(string token);

        /// <summary>
        /// Validates that a confirmation token is valid and not expired
        /// </summary>
        /// <param name="token">The confirmation token</param>
        /// <returns>True if the token is valid and not expired</returns>
        bool IsValidToken(string token);
    }
}

