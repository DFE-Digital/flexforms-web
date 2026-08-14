using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Notifications;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GovUK.Dfe.FlexForms.Web.Controllers
{
    [ApiController]
    [Route("notifications")]
    [Authorize]
    public class NotificationsController(
        INotificationsClient notificationsClient,
        IRequestAppConfiguration requestConfiguration) : ControllerBase
    {
        private string ApplicationContext =>
            requestConfiguration["ApplicationName"]
            ?? requestConfiguration["TenantName"]
            ?? throw new InvalidOperationException(
                "ApplicationName (or TenantName) is required in tenant configuration for notifications.");

        [HttpGet("unread")]
        public async Task<IActionResult> GetUnreadAsync(CancellationToken cancellationToken)
        {
            // Storage is already tenant-scoped; ApplicationName prefix hid API-created malware banners.
            return await ExecuteAsync(() => notificationsClient.GetUnreadNotificationsAsync(null, null, cancellationToken));
        }

        [HttpGet("all")]
        public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
        {
            return await ExecuteAsync(() => notificationsClient.GetAllNotificationsAsync(null, null, cancellationToken));
        }

        [ValidateAntiForgeryToken]
        [HttpPost("read/{id}")]
        public async Task<IActionResult> MarkAsReadAsync([FromRoute] string id, CancellationToken cancellationToken)
        {
            return await ExecuteAsync(async () =>
            {
                var ok = await notificationsClient.MarkNotificationAsReadAsync(id, cancellationToken);
                return ok;
            }, ApplicationAccessMessages.NoNotificationWritePermission);
        }

        [ValidateAntiForgeryToken]
        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllAsReadAsync(CancellationToken cancellationToken)
        {
            return await ExecuteAsync(async () =>
            {
                var ok = await notificationsClient.MarkAllNotificationsAsReadAsync(null, null, cancellationToken);
                return ok;
            }, ApplicationAccessMessages.NoNotificationWritePermission);
        }

        [ValidateAntiForgeryToken]
        [HttpPost("remove/{id}")]
        public async Task<IActionResult> RemoveAsync([FromRoute] string id, CancellationToken cancellationToken)
        {
            return await ExecuteAsync(async () =>
            {
                var ok = await notificationsClient.RemoveNotificationAsync(id, cancellationToken);
                return ok;
            }, ApplicationAccessMessages.NoNotificationDeletePermission);
        }

        [ValidateAntiForgeryToken]
        [HttpPost("clear")]
        public async Task<IActionResult> ClearAllAsync(CancellationToken cancellationToken)
        {
            return await ExecuteAsync(async () =>
            {
                var ok = await notificationsClient.ClearAllNotificationsAsync(cancellationToken);
                return ok;
            }, ApplicationAccessMessages.NoNotificationDeletePermission);
        }

        [ValidateAntiForgeryToken]
        [HttpPost("create")]
        public async Task<IActionResult> CreateAsync([FromBody] AddNotificationRequest request, CancellationToken cancellationToken)
        {
            request.Context = NotificationScopeContext.PrefixDetail(ApplicationContext, request.Context);
            return await ExecuteAsync(() => notificationsClient.CreateNotificationAsync(request, cancellationToken));
        }

        private async Task<IActionResult> ExecuteAsync<T>(Func<Task<T>> action)
        {
            try
            {
                return Ok(await action());
            }
            catch (ExternalApplicationsException ex) when (ex.StatusCode is 401 or 403)
            {
                return StatusCode(ex.StatusCode, new { message = ex.Message });
            }
        }

        private async Task<IActionResult> ExecuteAsync(Func<Task<bool>> action, string forbiddenMessage)
        {
            try
            {
                var ok = await action();
                return ok ? Ok() : Problem(statusCode: 500);
            }
            catch (ExternalApplicationsException ex) when (ex.StatusCode is 401 or 403)
            {
                return StatusCode(ex.StatusCode, new { message = forbiddenMessage });
            }
        }
    }
}
