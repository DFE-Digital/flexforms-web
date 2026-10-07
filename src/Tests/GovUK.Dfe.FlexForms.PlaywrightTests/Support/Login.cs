using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Support;

public static class Login
{
    public static async Task LoginAsync(IPage page, string? userName = null)
    {
        await SignInAsync(page, userName);
        await page.GotoAsync("/");
        await Assertions.Expect(page).ToHaveURLAsync(new Regex(@"(/applications/dashboard|/templates)"));
    }

    public static async Task SignInAsync(IPage page, string? userName = null)
    {
        var user = AuthUsers.ResolveAuthUser(userName);
        AuthenticationInterceptor.SetContextAuthUser(page.Context, user);

        await page.Context.ClearCookiesAsync();
        await page.Context.ClearPermissionsAsync();
        await page.Context.AddCookiesAsync(
        [
            new Cookie
            {
                Name = ".AspNet.Consent",
                Value = "yes",
                Url = TestConfig.GetServiceConfigFromEnv().Url,
            },
        ]);
    }
}