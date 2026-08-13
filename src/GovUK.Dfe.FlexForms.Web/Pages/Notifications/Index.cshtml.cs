using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GovUK.Dfe.FlexForms.Web.Pages.Notifications
{
    [Authorize]
    public class IndexModel : PageModel
    {
        public bool CanWriteNotifications { get; private set; }
        public bool CanDeleteNotifications { get; private set; }

        public void OnGet()
        {
            CanWriteNotifications = AdminAccessHelper.HasNotificationAccess(User, "Write");
            CanDeleteNotifications = AdminAccessHelper.HasNotificationAccess(User, "Delete");
        }
    }
}
