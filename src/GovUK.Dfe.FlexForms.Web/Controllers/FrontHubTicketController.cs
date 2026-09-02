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
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to mint SignalR hub ticket");
                return StatusCode(StatusCodes.Status503ServiceUnavailable);
            }
        }
    }
}
