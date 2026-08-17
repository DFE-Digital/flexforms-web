using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Caching;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class FormFileFieldServiceTests
{
    private readonly InMemoryFormSessionStore _session = new();
    private readonly IApplicationResponseService _responses = Substitute.For<IApplicationResponseService>();
    private readonly IInfectedFileStore _infectedFileStore = Substitute.For<IInfectedFileStore>();
    private readonly FormFileFieldService _service;
    private readonly Guid _applicationId = Guid.NewGuid();

    public FormFileFieldServiceTests()
    {
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object>());
        var progress = new CollectionFlowProgressStore(_session);
        var filter = new InfectedUploadFilter(_infectedFileStore, NullLogger<InfectedUploadFilter>.Instance);
        _service = new FormFileFieldService(
            _session,
            progress,
            filter,
            _infectedFileStore,
            _responses,
            NullLogger<FormFileFieldService>.Instance);
    }

    [Fact]
    public void GetFiles_reads_regular_upload_session_key()
    {
        var files = new List<UploadDto> { new() { Id = Guid.NewGuid(), OriginalFileName = "a.pdf" } };
        _session.SetString(FormSessionKeys.UploadedFiles(_applicationId, "evidence"), JsonSerializer.Serialize(files));

        var result = _service.GetFiles(new FormFileFieldContext(_applicationId, null, null), "evidence");

        Assert.Single(result);
        Assert.Equal("a.pdf", result[0].OriginalFileName);
    }

    [Fact]
    public void SaveFiles_then_GetFiles_round_trips_collection_progress()
    {
        var context = new FormFileFieldContext(_applicationId, "flow-1", "item-1");
        var files = new List<UploadDto> { new() { Id = Guid.NewGuid(), OriginalFileName = "b.pdf" } };

        _service.SaveFiles(context, "upload", files);
        var result = _service.GetFiles(context, "upload");

        Assert.Single(result);
        Assert.Equal("b.pdf", result[0].OriginalFileName);
    }

    [Fact]
    public void ReplaceUploadPlaceholders_uses_session_files_for_regular_forms()
    {
        var files = new List<UploadDto> { new() { Id = Guid.NewGuid(), OriginalFileName = "c.pdf" } };
        _session.SetString(FormSessionKeys.UploadedFiles(_applicationId, "upload"), JsonSerializer.Serialize(files));
        var data = new Dictionary<string, object>
        {
            ["upload"] = FormEngineConstants.UploadFieldSessionPlaceholder
        };

        _service.ReplaceUploadPlaceholders(data, new FormFileFieldContext(_applicationId, null, null));

        var stored = JsonSerializer.Deserialize<List<UploadDto>>(data["upload"].ToString()!);
        Assert.NotNull(stored);
        Assert.Equal("c.pdf", stored![0].OriginalFileName);
    }
}
