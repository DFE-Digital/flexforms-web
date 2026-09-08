using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class DeleteFormFileServiceTests
{
    private readonly IFormFileFieldService _fields = Substitute.For<IFormFileFieldService>();
    private readonly IFileUploadService _uploads = Substitute.For<IFileUploadService>();
    private readonly IApplicationResponseService _responses = Substitute.For<IApplicationResponseService>();
    private readonly DeleteFormFileService _service;

    public DeleteFormFileServiceTests()
    {
        _service = new DeleteFormFileService(_fields, _uploads, _responses, NullLogger<DeleteFormFileService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRedirectWithoutDeleting_WhenNotConfirmed()
    {
        var result = await _service.ExecuteAsync(
            new FormEngineWorkState { ReferenceNumber = "REF-1" },
            new DeleteFormFileRequest(Guid.NewGuid(), Guid.NewGuid(), "evidence", "/back", Confirmed: false));

        Assert.Equal("/back", result.RedirectUrl);
        await _uploads.DidNotReceive().DeleteFileAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDeleteAndStay_WhenConfirmedWithoutReturnUrl()
    {
        var appId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        _fields.GetFiles(Arg.Any<FormFileFieldContext>(), "evidence")
            .Returns([new UploadDto { Id = fileId, OriginalFileName = "a.pdf" }]);

        var result = await _service.ExecuteAsync(
            new FormEngineWorkState { ReferenceNumber = "REF-1", ApplicationId = appId },
            new DeleteFormFileRequest(appId, fileId, "evidence", ReturnUrl: null, Confirmed: true));

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal(FormEngineMessages.FileDeleted, result.SuccessMessage);
        await _uploads.Received().DeleteFileAsync(fileId, appId, Arg.Any<CancellationToken>());
        await _responses.Received().SaveApplicationResponseAsync(
            appId,
            Arg.Any<Dictionary<string, object>>(),
            Arg.Any<CancellationToken>());
    }
}

public class UploadFormFileServiceTests
{
    private readonly IFormFileFieldService _fields = Substitute.For<IFormFileFieldService>();
    private readonly IFileUploadService _uploads = Substitute.For<IFileUploadService>();
    private readonly IInfectedUploadFilter _infected = Substitute.For<IInfectedUploadFilter>();
    private readonly UploadFormFileService _service;

    public UploadFormFileServiceTests()
    {
        _infected.FilterList(Arg.Any<IReadOnlyList<UploadDto>>(), Arg.Any<string>())
            .Returns(call => call.ArgAt<IReadOnlyList<UploadDto>>(0).ToList());
        _service = new UploadFormFileService(_fields, _uploads, _infected, NullLogger<UploadFormFileService>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReject_WhenNoFile()
    {
        using var stream = new MemoryStream();
        var result = await _service.ExecuteAsync(
            new FormEngineWorkState { ReferenceNumber = "REF-1" },
            new UploadFormFileRequest(
                Guid.NewGuid(), "evidence", null, null, stream, "a.pdf", "application/pdf", "page", HasFile: false));

        Assert.Contains(result.Errors, e => e.Message == FormEngineMessages.SelectAFile);
        await _uploads.DidNotReceive().UploadFileAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<FileParameter>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRejectDuplicateFileName()
    {
        _fields.ContainsFileName(Arg.Any<FormFileFieldContext>(), "evidence", "a.pdf").Returns(true);
        using var stream = new MemoryStream();

        var result = await _service.ExecuteAsync(
            new FormEngineWorkState { ReferenceNumber = "REF-1" },
            new UploadFormFileRequest(
                Guid.NewGuid(), "evidence", "/back", null, stream, "a.pdf", "application/pdf", "page", HasFile: true));

        Assert.Equal("/back", result.RedirectUrl);
        Assert.Contains(result.Errors, e => e.Message == FormEngineMessages.DuplicateFileName);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldStay_WhenUploadSucceeds()
    {
        var appId = Guid.NewGuid();
        var uploaded = new UploadDto { Id = Guid.NewGuid(), OriginalFileName = "a.pdf" };
        _fields.GetFiles(Arg.Any<FormFileFieldContext>(), "evidence").Returns([]);
        _uploads.UploadFileAsync(appId, "a.pdf", null, Arg.Any<FileParameter>(), Arg.Any<CancellationToken>())
            .Returns(uploaded);
        using var stream = new MemoryStream("hi"u8.ToArray());

        var result = await _service.ExecuteAsync(
            new FormEngineWorkState { ReferenceNumber = "REF-1" },
            new UploadFormFileRequest(
                appId, "evidence", null, null, stream, "a.pdf", "application/pdf", "page", HasFile: true));

        Assert.Equal(FormEngineOutcomeKind.StayOnPage, result.Kind);
        Assert.Contains("uploaded", result.SuccessMessage);
        Assert.Equal($"file-upload|{uploaded.Id}", result.NotificationContext);
    }
}

public class DownloadFormFileServiceTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnFileWithDecodedName()
    {
        var uploads = Substitute.For<IFileUploadService>();
        var service = new DownloadFormFileService(uploads, NullLogger<DownloadFormFileService>.Instance);
        using var stream = new MemoryStream([1, 2, 3]);
        var headers = new Dictionary<string, IEnumerable<string>>
        {
            ["Content-Type"] = ["application/pdf"],
            ["Content-Disposition"] = ["attachment; filename=\"report.pdf\""]
        };
        uploads.DownloadFileAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(new FileResponse(200, headers, stream, Substitute.For<IDisposable>(), Substitute.For<IDisposable>()));

        var result = await service.ExecuteAsync(
            new FormEngineWorkState { ReferenceNumber = "REF-1" },
            new DownloadFormFileRequest(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(FormEngineOutcomeKind.FileDownload, result.Kind);
        Assert.Equal("report.pdf", result.FileDownloadName);
        Assert.Equal("application/pdf", result.FileContentType);
    }
}
