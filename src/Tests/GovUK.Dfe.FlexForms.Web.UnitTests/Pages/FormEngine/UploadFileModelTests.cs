using System.Diagnostics.CodeAnalysis;
using System.Text;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Interfaces;
using GovUK.Dfe.FlexForms.Web.Pages.FormEngine;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Pages.FormEngine;

public class UploadFileModelTests
{
    private readonly IUploadFormFile _upload = Substitute.For<IUploadFormFile>();
    private readonly ITemplateManagementService _templates = Substitute.For<ITemplateManagementService>();
    private readonly IFormErrorStore _errorStore = Substitute.For<IFormErrorStore>();
    private readonly InMemorySession _session = new();
    private readonly UploadFileModel _model;

    public UploadFileModelTests()
    {
        _upload.ExecuteAsync(Arg.Any<FormEngineWorkState>(), Arg.Any<UploadFormFileRequest>(), Arg.Any<CancellationToken>())
            .Returns(FormEngineOutcome.Stay());

        var config = Substitute.For<IRequestAppConfiguration>();
        config["ApplicationName"].Returns("Test");

        _model = new UploadFileModel(
            _upload,
            Substitute.For<IDeleteFormFile>(),
            Substitute.For<IDownloadFormFile>(),
            Substitute.For<IFormFileFieldService>(),
            Substitute.For<IFileUploadService>(),
            Substitute.For<IApplicationResponseService>(),
            Substitute.For<IApplicationStateService>(),
            _templates,
            Substitute.For<INotificationsClient>(),
            _errorStore,
            config,
            NullLogger<UploadFileModel>.Instance)
        {
            ApplicationId = Guid.NewGuid().ToString(),
            FieldId = "evidence",
            ReferenceNumber = "REF-1",
            TaskId = "t1",
            ReturnUrl = "/applications/REF-1/t1/p1"
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Features.Set<ISessionFeature>(new StubSessionFeature { Session = _session });
        var file = new FormFile(new MemoryStream("hi"u8.ToArray()), 0, 2, "ChooseFile", "a.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };
        httpContext.Request.Form = new FormCollection(
            new Dictionary<string, StringValues>(),
            new FormFileCollection { file });

        _model.PageContext = new PageContext
        {
            HttpContext = httpContext,
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        };
    }

    [Fact]
    public async Task OnPostUploadFileAsync_ShouldNotUpload_WhenNoTemplateIsSelected()
    {
        var result = await _model.OnPostUploadFileAsync();

        Assert.Equal("/applications/REF-1/t1/p1", Assert.IsType<RedirectResult>(result).Url);
        Assert.Equal(FormEngineMessages.UploadFormUnavailable, _model.ErrorMessage);
        _errorStore.Received(1).Save("evidence", Arg.Any<ModelStateDictionary>(), FormEngineMessages.UploadFormUnavailable);
        await _upload.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default!, default);
    }

    [Fact]
    public async Task OnPostUploadFileAsync_ShouldNotUpload_WhenTheTemplateFailsToLoad()
    {
        _session.SetString("TemplateId", "tpl");
        _templates.LoadTemplateAsync("tpl", Arg.Any<ApplicationDto?>()).ThrowsAsync(new HttpRequestException("api down"));
        _model.ReturnUrl = string.Empty;

        var result = await _model.OnPostUploadFileAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(FormEngineMessages.UploadFormUnavailable, _model.ErrorMessage);
        await _upload.DidNotReceiveWithAnyArgs().ExecuteAsync(default!, default!, default);
    }

    [Fact]
    public async Task OnPostUploadFileAsync_ShouldUploadWithTheTemplate_WhenItLoads()
    {
        var template = new FormTemplate { TemplateId = "tpl", TemplateName = "Template", Description = "", TaskGroups = [] };
        _session.SetString("TemplateId", "tpl");
        _templates.LoadTemplateAsync("tpl", Arg.Any<ApplicationDto?>()).Returns(template);

        await _model.OnPostUploadFileAsync();

        await _upload.Received(1).ExecuteAsync(
            Arg.Is<FormEngineWorkState>(state => state.Template == template && state.TaskId == "t1"),
            Arg.Is<UploadFormFileRequest>(request => request.FieldId == "evidence" && request.HasFile),
            Arg.Any<CancellationToken>());
    }

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

        public void Clear() => _store.Clear();

        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Remove(string key) => _store.Remove(key);

        public void Set(string key, byte[] value) => _store[key] = value;

        public bool TryGetValue(string key, [NotNullWhen(true)] out byte[]? value)
            => _store.TryGetValue(key, out value);

        public void SetString(string key, string value) => Set(key, Encoding.UTF8.GetBytes(value));
    }
}
