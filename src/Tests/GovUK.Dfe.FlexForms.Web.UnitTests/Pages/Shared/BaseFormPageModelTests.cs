using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using GovUK.Dfe.FlexForms.Web.Authentication;
using GovUK.Dfe.FlexForms.Web.Pages.Shared;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;
using TaskStatus = GovUK.Dfe.FlexForms.Domain.Models.TaskStatus;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.Shared;

public sealed class BaseFormPageModelTests
{
    private static readonly Guid ApplicationId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private readonly IApplicationResponseService _responses = Substitute.For<IApplicationResponseService>();
    private readonly IFieldFormattingService _formatting = Substitute.For<IFieldFormattingService>();
    private readonly ITemplateManagementService _templates = Substitute.For<ITemplateManagementService>();
    private readonly IApplicationStateService _state = Substitute.For<IApplicationStateService>();
    private readonly InMemorySession _session = new();

    public BaseFormPageModelTests()
    {
        // Use the real status rule, so these tests break if what counts as editable changes.
        var realState = new ApplicationStateService(
            Substitute.For<GovUK.Dfe.FlexForms.Api.Client.Contracts.IApplicationsClient>(),
            _responses,
            Substitute.For<IFieldRequirementService>(),
            Substitute.For<IFormSessionStore>(),
            NullLogger<ApplicationStateService>.Instance);
        _state.IsApplicationEditable(Arg.Any<string>())
            .Returns(call => realState.IsApplicationEditable(call.Arg<string>()));
    }

    [Theory]
    [InlineData("Admin", "Submitted")]
    [InlineData("Admin", "Deleted")]
    [InlineData("SuperAdmin", "Submitted")]
    [InlineData("SuperAdmin", "Deleted")]
    public void Admin_CanEditInAnyStatus(string role, string status)
    {
        var model = CreateModel(SignedIn(role), status);

        Assert.True(model.IsApplicationEditable());
    }

    [Theory]
    [InlineData("Created", true)]
    [InlineData("InProgress", true)]
    [InlineData("Submitted", false)]
    [InlineData("Deleted", false)]
    public void UserWithWriteGrant_CanEditOnlyBeforeSubmission(string status, bool expected)
    {
        var model = CreateModel(SignedIn("User", $"Application:{ApplicationId}:Write"), status);

        Assert.Equal(expected, model.IsApplicationEditable());
    }

    [Theory]
    [InlineData("Application:Any:Read")]
    [InlineData("Application:aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa:Read")]
    public void UserWithoutWriteGrantWhoIsNotTheLeadApplicant_CannotEdit(string grant)
    {
        var model = CreateModel(SignedIn("Caseworker", grant), "InProgress");

        Assert.False(model.IsApplicationEditable());
    }

    [Theory]
    [InlineData("Created", true)]
    [InlineData("InProgress", true)]
    [InlineData("Submitted", false)]
    public void LeadApplicantInSession_CanEditBeforeSubmission_WithoutAWriteGrantYet(string status, bool expected)
    {
        _session.SetString($"ApplicationLeadApplicantUserId_{ApplicationId}", "USER-1");
        var model = CreateModel(SignedIn("User"), status);

        Assert.Equal(expected, model.IsApplicationEditable());
    }

    [Fact]
    public void LeadApplicantMatchedByEmail_IgnoringCaseAndSpaces_CanEdit()
    {
        _session.SetString($"ApplicationLeadApplicantEmail_{ApplicationId}", " Lead@Example.com ");
        var model = CreateModel(SignedIn("User"), "InProgress");

        Assert.True(model.IsApplicationEditable());
    }

    [Fact]
    public void SomeoneElseAsLeadApplicant_DoesNotLetTheUserEdit()
    {
        _session.SetString($"ApplicationLeadApplicantUserId_{ApplicationId}", "user-2");
        _session.SetString($"ApplicationLeadApplicantEmail_{ApplicationId}", "other@example.com");
        var model = CreateModel(SignedIn("User"), "InProgress");

        Assert.False(model.IsApplicationEditable());
    }

    [Fact]
    public void NoApplicationLoaded_IsNotEditable()
    {
        var model = CreateModel(SignedIn("User", $"Application:{ApplicationId}:Write"), "InProgress");
        model.ApplicationId = null;

        Assert.False(model.IsApplicationEditable());
    }

    [Fact]
    public void ServiceAuthenticatedCallerWithAnAdminRole_IsNotTreatedAsAnAdmin()
    {
        var serviceCaller = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "service-1"), new Claim(ClaimTypes.Role, "Admin")],
            InternalServiceAuthenticationHandler.SchemeName));
        var model = CreateModel(serviceCaller, "Submitted");

        Assert.False(model.IsUserAdmin());
        Assert.False(model.IsApplicationEditable());
        Assert.True(CreateModel(SignedIn("Admin"), "Submitted").IsUserAdmin());
    }

    [Theory]
    [InlineData(TaskStatus.Completed, "govuk-tag--green", "Completed")]
    [InlineData(TaskStatus.InProgress, "govuk-tag--blue", "In progress")]
    [InlineData(TaskStatus.NotStarted, "govuk-tag--grey", "Not started")]
    [InlineData(TaskStatus.CannotStartYet, "govuk-tag--orange", "Cannot start yet")]
    [InlineData((TaskStatus)99, "govuk-tag--grey", "Not started")]
    public void TaskStatusDisplay(TaskStatus status, string cssClass, string text)
    {
        var model = CreateModel(SignedIn("User"), "InProgress");

        Assert.Equal(cssClass, model.GetTaskStatusDisplayClass(status));
        Assert.Equal(text, model.GetTaskStatusDisplayText(status));
    }

    [Fact]
    public void FieldAndTaskHelpers_PassTheLoadedFormDataAndTemplateToTheServices()
    {
        var model = CreateModel(SignedIn("User"), "InProgress");
        var template = Template();
        model.Template = template;
        model.FormData = new Dictionary<string, object> { ["name"] = "Ada" };
        _formatting.GetFieldValue("name", model.FormData).Returns("Ada");
        _formatting.GetFormattedFieldValue("name", model.FormData).Returns("Ada!");
        _formatting.GetFormattedFieldValues("name", model.FormData).Returns(["Ada"]);
        _formatting.GetFieldItemLabel("name", template).Returns("Name");
        _formatting.IsFieldAllowMultiple("name", template).Returns(true);
        _formatting.HasFieldValue("name", model.FormData).Returns(true);
        _state.CalculateTaskStatus("task-1", template, model.FormData, ApplicationId, "InProgress").Returns(TaskStatus.Completed);
        _state.AreAllTasksCompleted(template, model.FormData, ApplicationId, "InProgress").Returns(true);
        var missing = new Dictionary<string, List<string>> { ["task-1"] = ["name"] };
        _state.ValidateAllRequiredFieldsForSubmission(template, model.FormData, null).Returns(missing);

        Assert.Equal("Ada", model.GetFieldValue("name"));
        Assert.Equal("Ada!", model.GetFormattedFieldValue("name"));
        Assert.Equal(["Ada"], model.GetFormattedFieldValues("name"));
        Assert.Equal("Name", model.GetFieldItemLabel("name"));
        Assert.True(model.IsFieldAllowMultiple("name"));
        Assert.True(model.HasFieldValue("name"));
        Assert.Equal(TaskStatus.Completed, model.GetTaskStatusFromSession("task-1"));
        Assert.True(model.AreAllTasksCompleted());
        Assert.Same(missing, model.ValidateAllRequiredFieldsForSubmission());
        Assert.True(model.IsContributorPatternEnabled());

        model.FindTask("task-1");
        model.FindPage("page-1");
        _templates.Received(1).FindTask(template, "task-1");
        _templates.Received(1).FindPage(template, "page-1");
    }

    [Fact]
    public async Task CommonInitialization_LoadsTheApplicationTemplateAnswersAndStatus()
    {
        var model = CreateModel(SignedIn("User"), status: "Created");
        model.ApplicationId = null;
        model.ReferenceNumber = "APP-001";
        _session.SetString("TemplateId", "template-1");
        var template = Template();
        var answers = new Dictionary<string, object> { ["name"] = "Ada" };
        _state.EnsureApplicationIdAsync("APP-001").Returns((ApplicationId, (ApplicationDto?)null));
        _templates.LoadTemplateAsync("template-1", null).Returns(template);
        _responses.GetAccumulatedFormData().Returns(answers);
        _state.GetApplicationStatus(ApplicationId).Returns("Submitted");

        await model.InitialiseAsync();

        Assert.Equal("template-1", model.TemplateId);
        Assert.Equal(ApplicationId, model.ApplicationId);
        Assert.Same(template, model.Template);
        Assert.Same(answers, model.FormData);
        Assert.Equal("Submitted", model.ApplicationStatus);
    }

    [Fact]
    public async Task CommonInitialization_StopsAndRethrowsWhenTheApplicationCannotBeLoaded()
    {
        var model = CreateModel(SignedIn("User"), "InProgress");
        _state.EnsureApplicationIdAsync(Arg.Any<string>()).ThrowsAsync(new InvalidOperationException("API down"));

        await Assert.ThrowsAsync<InvalidOperationException>(model.InitialiseAsync);

        await _templates.DidNotReceiveWithAnyArgs().LoadTemplateAsync(default!, default);
    }

    private TestFormPageModel CreateModel(ClaimsPrincipal user, string status)
    {
        var httpContext = new DefaultHttpContext
        {
            User = user,
            RequestServices = new ServiceCollection().BuildServiceProvider()
        };
        httpContext.Session = _session;

        return new TestFormPageModel(_responses, _formatting, _templates, _state)
        {
            PageContext = new PageContext { HttpContext = httpContext },
            ApplicationId = ApplicationId,
            ApplicationStatus = status
        };
    }

    private static ClaimsPrincipal SignedIn(string role, params string[] grants) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "user-1"),
                new Claim(ClaimTypes.Email, "lead@example.com"),
                new Claim(ClaimTypes.Role, role),
                .. grants.Select(g => new Claim("permission", g))
            ],
            "Cookies"));

    private static FormTemplate Template() => new()
    {
        TemplateId = "template-1",
        TemplateName = "Test",
        Description = "Test",
        TaskGroups = []
    };

    private sealed class TestFormPageModel(
        IApplicationResponseService responses,
        IFieldFormattingService formatting,
        ITemplateManagementService templates,
        IApplicationStateService state)
        : BaseFormPageModel(
            Substitute.For<IFieldRendererService>(),
            responses,
            formatting,
            templates,
            state,
            NullLogger.Instance)
    {
        public Task InitialiseAsync() => CommonInitializationAsync();

        public void FindTask(string taskId) => InitializeCurrentTask(taskId);

        public void FindPage(string pageId) => InitializeCurrentPage(pageId);
    }

    private sealed class InMemorySession : ISession
    {
        private readonly Dictionary<string, byte[]> _values = [];

        public bool IsAvailable => true;
        public string Id => "session-1";
        public IEnumerable<string> Keys => _values.Keys;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value) => _values.TryGetValue(key, out value);
        public void Set(string key, byte[] value) => _values[key] = value;
        public void Remove(string key) => _values.Remove(key);
        public void Clear() => _values.Clear();
    }
}
