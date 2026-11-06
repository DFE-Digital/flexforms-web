using System.Reflection;
using GovUK.Dfe.FlexForms.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Controllers;

public class FileValidationMessageControllerTests
{
    [Fact]
    public void Render_ShouldReturnSanitisedHtml_ForMarkdownMessage()
    {
        var controller = new FileValidationMessageController();

        var result = Assert.IsType<OkObjectResult>(controller.Render(
            new FileValidationMessageController.RenderRequest { Message = "Sheet **Budget** <script>x</script>" }));

        var html = (string)result.Value!.GetType().GetProperty("html")!.GetValue(result.Value)!;
        var isInline = (bool)result.Value.GetType().GetProperty("isInline")!.GetValue(result.Value)!;
        Assert.Contains("<strong>Budget</strong>", html);
        Assert.DoesNotContain("<script", html);
        Assert.True(isInline);
    }

    [Fact]
    public void Render_ShouldRequireAuthenticationAndAntiforgery()
    {
        Assert.NotNull(typeof(FileValidationMessageController).GetCustomAttribute<AuthorizeAttribute>());
        Assert.NotNull(typeof(FileValidationMessageController)
            .GetMethod(nameof(FileValidationMessageController.Render))!
            .GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }
}
