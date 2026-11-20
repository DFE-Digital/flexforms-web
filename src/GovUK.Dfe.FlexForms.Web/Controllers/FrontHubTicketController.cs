using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GovUK.Dfe.FlexForms.Web.Controllers
{
    /// <summary>
    /// Anonymous on purpose. notifications.js asks for a ticket on every page, including the anonymous
    /// error and feedback pages; with [Authorize] those background fetches would start an OIDC sign-in
    /// challenge. For a signed-out caller Api.Client's TokenExchangeHandler answers 401 without calling the
    /// API (which only mints tickets for a user token with an email), and this returns that 401, so the page
    /// carries on without live notifications. ExpectedPageSecurity.json pins this as anonymous.
    /// </summary>
    [ApiController]
    [AllowAnonymous]
    [Route("internal/hub-ticket")]
    public class FrontHubTicketController(
        IHubAuthClient hubAuthClient,
        ILogger<FrontHubTicketController> logger) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            try
            {
                var resp = await hubAuthClient.CreateHubTicketAsync();
                return Ok(resp);
            }
            catch (ExternalApplicationsException ex) when (ex.StatusCode is 401 or 403)
            {
                // Expired or revoked API session; the page carries on without live notifications.
                logger.LogInformation("SignalR hub ticket refused by the API with {StatusCode}", ex.StatusCode);
                return StatusCode(ex.StatusCode);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to mint SignalR hub ticket");
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }
    }
}
