using GovUK.Dfe.CoreLibs.Testing.Mocks.Session;
using GovUK.Dfe.FlexForms.Web.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Security;

public class UserActivityTrackerTests
{
    private readonly UserActivityTracker _tracker = new(NullLogger<UserActivityTracker>.Instance);

    [Fact]
    public void RecordActivity_ShouldSetLastActivityAndSessionStart()
    {
        var http = ContextWithSession();

        _tracker.RecordActivity(http);

        Assert.False(string.IsNullOrEmpty(http.Session.GetString("Session:LastActivity")));
        Assert.False(string.IsNullOrEmpty(http.Session.GetString("Session:StartTime")));
        Assert.False(_tracker.IsUserInactive(http, 20));
        Assert.False(_tracker.HasSessionExpired(http, 8));
    }

    [Fact]
    public void RecordSessionStart_ShouldNotOverwriteExistingStart()
    {
        var http = ContextWithSession();
        _tracker.RecordActivity(http);
        var originalStart = http.Session.GetString("Session:StartTime");

        _tracker.RecordSessionStart(http);

        Assert.Equal(originalStart, http.Session.GetString("Session:StartTime"));
    }

    [Fact]
    public void GetTimeSinceLastActivity_ShouldReturnNull_WhenNoActivityRecorded()
    {
        var http = ContextWithSession();

        Assert.Null(_tracker.GetTimeSinceLastActivity(http));
        Assert.Null(_tracker.GetSessionDuration(http));
        Assert.False(_tracker.IsUserInactive(http, 1));
        Assert.False(_tracker.HasSessionExpired(http, 1));
    }

    [Fact]
    public void GetTimeSinceLastActivity_ShouldReturnNull_WhenTimestampIsInvalid()
    {
        var http = ContextWithSession();
        http.Session.SetString("Session:LastActivity", "not-a-date");
        http.Session.SetString("Session:StartTime", "also-bad");

        Assert.Null(_tracker.GetTimeSinceLastActivity(http));
        Assert.Null(_tracker.GetSessionDuration(http));
    }

    [Fact]
    public void IsUserInactive_ShouldBeTrue_WhenLastActivityIsOlderThanThreshold()
    {
        var http = ContextWithSession();
        http.Session.SetString("Session:LastActivity", DateTime.UtcNow.AddMinutes(-30).ToString("o"));
        http.Session.SetString("Session:StartTime", DateTime.UtcNow.AddHours(-10).ToString("o"));

        Assert.True(_tracker.IsUserInactive(http, 20));
        Assert.True(_tracker.HasSessionExpired(http, 8));
    }

    [Fact]
    public void RecordActivity_ShouldNoOp_WhenSessionIsUnavailable()
    {
        var http = new DefaultHttpContext();

        _tracker.RecordActivity(http);
        _tracker.RecordSessionStart(http);

        Assert.Null(_tracker.GetTimeSinceLastActivity(http));
        Assert.Null(_tracker.GetSessionDuration(http));
    }

    private static DefaultHttpContext ContextWithSession() =>
        new() { Session = new InMemorySession() };
}
