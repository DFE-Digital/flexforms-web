using System.ComponentModel.DataAnnotations;
using GovUK.Dfe.FlexForms.Web.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GovUK.Dfe.FlexForms.Web.Controllers
{
    /// <summary>
    /// Renders a file-validation message (Markdown from the tenant function) for live SignalR updates,
    /// so the browser never parses Markdown or inserts unsanitised HTML itself.
    /// </summary>
    [ApiController]
    [Route("file-validation/message")]
    [Authorize]
    public class FileValidationMessageController : ControllerBase
    {
        public sealed class RenderRequest
        {
            [StringLength(RenderedMaxChars)]
            public string? Message { get; set; }
        }

        private const int RenderedMaxChars = 2000;

        [ValidateAntiForgeryToken]
        [HttpPost("render")]
        public IActionResult Render([FromBody] RenderRequest request)
        {
            var (html, isInline) = MarkdownSafe.RenderIntegrationMessage(request.Message, RenderedMaxChars);
            return Ok(new { html, isInline });
        }
    }
}
