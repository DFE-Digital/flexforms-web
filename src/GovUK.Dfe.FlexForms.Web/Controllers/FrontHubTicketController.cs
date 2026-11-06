using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace GovUK.Dfe.FlexForms.Web.Controllers
{
    [ApiController]
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
