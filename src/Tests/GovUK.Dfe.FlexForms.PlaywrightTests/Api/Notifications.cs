using Microsoft.Playwright;

namespace GovUK.Dfe.FlexForms.PlaywrightTests.Api;

public static class Notifications
{
    public static Task<NotificationDto> AddNotificationAsync(
        IAPIRequestContext request,
        string message,
        NotificationType type) =>
        ApiBase.ApiRequestAsync<NotificationDto>(request, "/v1/notifications", method: "POST",
            data: new AddNotificationRequest(
                message,
                type));

    public static Task ClearAllNotificationsAsync(IAPIRequestContext request) =>
        ApiBase.ApiRequestAsync(request, "/v1/notifications/clear-all", method: "DELETE");
}