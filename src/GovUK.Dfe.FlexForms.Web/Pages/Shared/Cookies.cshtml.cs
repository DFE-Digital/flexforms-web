using System.Net;
using Microsoft.ApplicationInsights.AspNetCore.Extensions;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Shared
{
    public class CookiesModel : PageModel
    {
        private readonly string cookieDomain;
        private readonly ILogger<CookiesModel> logger;

        public CookiesModel(ILogger<CookiesModel> logger, IConfiguration configuration)
        {
            this.cookieDomain = configuration["ApplicationInsights:CookieDomain"];
            this.logger = logger;
        }
        public void OnGet(string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl))
                TempData["returnUrl"] = returnUrl;

        }

        public IActionResult OnPostHideMessage(string? redirectPath)
        {
            HttpContext.Session.SetString("CookieBannerHidden", "1");
            if (string.IsNullOrWhiteSpace(redirectPath))
            {
                redirectPath = "/";
            }

            return LocalRedirect(redirectPath);
        }

        public async Task<IActionResult> OnPostAsync(CookieConsent cookies, string redirectPath, string returnPath)
        {
            var uri = Request.GetUri();

            this.logger.LogInformation($"Host: {uri.Host} | Uri.ToString() : {uri.ToString} | Absolute Uri : {uri.AbsoluteUri} | Authority: {uri.Authority}");
            switch (cookies)
            {
                case CookieConsent.Accept:
                    HttpContext.Session.Remove("cookiesRejected");
                    SetConsentCookie("yes");
                    break;
                case CookieConsent.Reject:
                    HttpContext.Session.SetInt32("cookiesRejected", 1);
                    SetConsentCookie("no");
                    Response.Cookies.Delete("ai_", new CookieOptions { Domain = this.cookieDomain, Path = "/" });
                    var appInsightsCookie = Request.Cookies.Keys.FirstOrDefault(key => key.StartsWith("ai_"));
                    if (!string.IsNullOrEmpty(appInsightsCookie))
                        Response.Cookies.Delete(appInsightsCookie, new CookieOptions { Domain = this.cookieDomain, Path = "/" });
                    break;
                    // No default because if we get a value out of range then we can just ignore it
            }

            TempData["cookiePreferenceSaved"] = true;
            TempData["returnPath"] = returnPath;
            return Redirect(redirectPath);
        }

        private void SetConsentCookie(string value) =>
            Response.Cookies.Append(".AspNet.Consent", value,
                new CookieOptions { Expires = DateTime.Now + TimeSpan.FromDays(365), Path = "/", Secure = true, HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true });
    }

    public enum CookieConsent
    {
        Unknown,
        Accept,
        Reject
    }
}
