using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Mvc;

namespace GovUK.Dfe.FlexForms.Web.Controllers
{
    [ApiController]
    [Route("internal/hub-ticket")]
    public class FrontHubTicketController(IHubAuthClient hubAuthClient) : ControllerBase
    {

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            var resp = await hubAuthClient.CreateHubTicketAsync();

            return Ok(resp);
        }
    }
}
