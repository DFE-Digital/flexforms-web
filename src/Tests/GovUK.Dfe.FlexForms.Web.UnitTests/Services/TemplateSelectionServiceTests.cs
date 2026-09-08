using System.Diagnostics.CodeAnalysis;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class TemplateSelectionServiceTests
{
    private const string TemplateIdSessionKey = "TemplateId";
    private const string TemplateNameSessionKey = "TemplateName";
    private const string TemplateIsLiveSessionKey = "TemplateIsLive";

    private readonly ITemplatesClient _templatesClient = Substitute.For<ITemplatesClient>();

    [Fact]
    public async Task GetSelectableTemplatesAsync_ShouldOrderLiveFirstThenByNameIgnoringCase()
    {
        var draftAlpha = CreateTemplate("alpha", isLive: false);
        var liveZulu = CreateTemplate("Zulu", isLive: true);
        var liveBravo = CreateTemplate("bravo", isLive: true);

        _templatesClient.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>())
            .Returns([draftAlpha, liveZulu, liveBravo]);

        var result = await CreateService().GetSelectableTemplatesAsync();

        Assert.Equal(
            new[] { liveBravo.TemplateId, liveZulu.TemplateId, draftAlpha.TemplateId },
            result.Select(t => t.TemplateId));
    }

    [Fact]
    public async Task GetSelectableTemplatesAsync_ShouldReturnEmpty_WhenNoTemplatesAccessible()
    {
        _templatesClient.GetAccessibleTemplatesAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateService().GetSelectableTemplatesAsync();

        Assert.Empty(result);
    }

    [Fact]
    public void GetSelectedTemplateId_ShouldReturnNull_WhenNothingSelected()
    {
        var (context, _) = CreateContext();

        Assert.Null(CreateService().GetSelectedTemplateId(context));
    }

    [Fact]
    public async Task SelectTemplateAsync_ShouldStoreSelectionAndCommitSession()
    {
        var (context, session) = CreateContext();
        var template = CreateTemplate("Transfers", isLive: true);

        await CreateService().SelectTemplateAsync(context, template);

        Assert.Equal(template.TemplateId.ToString(), session.GetString(TemplateIdSessionKey));
        Assert.Equal("Transfers", session.GetString(TemplateNameSessionKey));
        Assert.Equal("True", session.GetString(TemplateIsLiveSessionKey));
        Assert.Equal(1, session.CommitCount);
    }

    [Fact]
    public async Task SelectTemplateAsync_ShouldClearApplicationState_WhenSwitchingToADifferentTemplate()
    {
        var (context, session) = CreateContext();
        session.SetString(TemplateIdSessionKey, Guid.NewGuid().ToString());
        session.SetString("ApplicationId", "app-1");
        session.SetString("ApplicationReference", "REF-1");
        session.SetString("FormData", "{}");
        session.SetString("CurrentTaskId", "task-1");
        session.SetString("CurrentPageId", "page-1");
        session.SetString("ApplicationStatus_app-1", "InProgress");
        session.SetString("FormAccumulation_app-1", "{}");
        session.SetString("UnrelatedKey", "keep-me");

        await CreateService().SelectTemplateAsync(context, CreateTemplate("Transfers", isLive: true));

        Assert.Null(session.GetString("ApplicationId"));
        Assert.Null(session.GetString("ApplicationReference"));
        Assert.Null(session.GetString("FormData"));
        Assert.Null(session.GetString("CurrentTaskId"));
        Assert.Null(session.GetString("CurrentPageId"));
        Assert.Null(session.GetString("ApplicationStatus_app-1"));
        Assert.Null(session.GetString("FormAccumulation_app-1"));
        Assert.Equal("keep-me", session.GetString("UnrelatedKey"));
    }

    [Fact]
    public async Task SelectTemplateAsync_ShouldKeepApplicationState_WhenReselectingTheSameTemplate()
    {
        var (context, session) = CreateContext();
        var template = CreateTemplate("Transfers", isLive: true);
        session.SetString(TemplateIdSessionKey, template.TemplateId.ToString());
        session.SetString("ApplicationId", "app-1");

        await CreateService().SelectTemplateAsync(context, template);

        Assert.Equal("app-1", session.GetString("ApplicationId"));
    }

    [Fact]
    public void HasValidSelection_ShouldReturnFalse_WhenNothingSelected()
    {
        var (context, _) = CreateContext();

        Assert.False(CreateService().HasValidSelection(context, [CreateTemplate("Transfers", isLive: true)]));
    }

    [Fact]
    public void HasValidSelection_ShouldReturnFalse_WhenSelectionIsNotAGuid()
    {
        var (context, session) = CreateContext();
        session.SetString(TemplateIdSessionKey, "not-a-guid");

        Assert.False(CreateService().HasValidSelection(context, [CreateTemplate("Transfers", isLive: true)]));
    }

    [Fact]
    public void HasValidSelection_ShouldReturnFalse_WhenSelectedTemplateIsNoLongerAccessible()
    {
        var (context, session) = CreateContext();
        session.SetString(TemplateIdSessionKey, Guid.NewGuid().ToString());

        Assert.False(CreateService().HasValidSelection(context, [CreateTemplate("Transfers", isLive: true)]));
    }

    [Fact]
    public void HasValidSelection_ShouldReturnTrue_WhenSelectedTemplateIsAccessible()
    {
        var (context, session) = CreateContext();
        var template = CreateTemplate("Transfers", isLive: true);
        session.SetString(TemplateIdSessionKey, template.TemplateId.ToString());

        Assert.True(CreateService().HasValidSelection(context, [template]));
    }

    [Theory]
    [InlineData("False", true)]
    [InlineData("True", false)]
    [InlineData("not-a-bool", false)]
    public void IsPreviewSelection_ShouldReflectTheStoredIsLiveFlag(string storedFlag, bool expected)
    {
        var (context, session) = CreateContext();
        session.SetString(TemplateIsLiveSessionKey, storedFlag);

        Assert.Equal(expected, CreateService().IsPreviewSelection(context));
    }

    [Fact]
    public void IsPreviewSelection_ShouldReturnFalse_WhenFlagMissing()
    {
        var (context, _) = CreateContext();

        Assert.False(CreateService().IsPreviewSelection(context));
    }

    [Fact]
    public void GetSelectedTemplateName_ShouldReturnStoredName()
    {
        var (context, session) = CreateContext();
        session.SetString(TemplateNameSessionKey, "Transfers");

        Assert.Equal("Transfers", CreateService().GetSelectedTemplateName(context));
    }

    private TemplateSelectionService CreateService()
        => new(_templatesClient, NullLogger<TemplateSelectionService>.Instance);

    private static (DefaultHttpContext Context, InMemorySession Session) CreateContext()
    {
        var session = new InMemorySession();
        var context = new DefaultHttpContext();
        context.Features.Set<ISessionFeature>(new StubSessionFeature { Session = session });
        return (context, session);
    }

    private static TemplateDto CreateTemplate(string name, bool isLive)
        => new()
        {
            TemplateId = Guid.NewGuid(),
            Name = name,
            CreatedOn = DateTime.UtcNow,
            IsLive = isLive
        };

    private sealed class StubSessionFeature : ISessionFeature
    {
        public ISession Session { get; set; } = new InMemorySession();
    }

    private sealed class InMemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = [];

        public bool IsAvailable => true;

        public string Id => "test-session";

        public IEnumerable<string> Keys => _store.Keys;

        public int CommitCount { get; private set; }

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value)
            => _store.TryGetValue(key, out value);
    }
}
