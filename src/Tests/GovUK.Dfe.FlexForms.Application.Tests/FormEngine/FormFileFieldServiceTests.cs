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

    [Fact]
    public void GetFiles_returns_empty_when_field_id_is_missing()
    {
        var result = _service.GetFiles(new FormFileFieldContext(_applicationId, null, null), "");
        Assert.Empty(result);
    }

    [Fact]
    public void GetFiles_filters_infected_files_from_session()
    {
        var clean = new UploadDto { Id = Guid.NewGuid(), OriginalFileName = "ok.pdf" };
        var infected = new UploadDto { Id = Guid.NewGuid(), OriginalFileName = "bad.pdf" };
        _infectedFileStore.IsFileInfected(infected.Id).Returns(true);
        _session.SetString(
            FormSessionKeys.UploadedFiles(_applicationId, "evidence"),
            JsonSerializer.Serialize(new[] { clean, infected }));

        var result = _service.GetFiles(new FormFileFieldContext(_applicationId, null, null), "evidence");

        Assert.Single(result);
        Assert.Equal(clean.Id, result[0].Id);
    }

    [Fact]
    public void GetFiles_reads_collection_item_from_accumulated_data_when_progress_is_empty()
    {
        var file = new UploadDto { Id = Guid.NewGuid(), OriginalFileName = "item.pdf" };
        var items = JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, object>
            {
                ["id"] = "item-1",
                ["upload"] = JsonSerializer.Serialize(new[] { file })
            }
        });
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object> { ["members"] = items });

        var result = _service.GetFiles(new FormFileFieldContext(_applicationId, "flow-1", "item-1"), "upload");

        Assert.Single(result);
        Assert.Equal("item.pdf", result[0].OriginalFileName);
    }

    [Fact]
    public void ReplaceUploadPlaceholders_uses_collection_progress_then_filters()
    {
        var files = new List<UploadDto> { new() { Id = Guid.NewGuid(), OriginalFileName = "progress.pdf" } };
        var context = new FormFileFieldContext(_applicationId, "flow-1", "item-1");
        _service.SaveFiles(context, "upload", files);
        var data = new Dictionary<string, object>
        {
            ["upload"] = FormEngineConstants.UploadFieldSessionPlaceholder
        };

        _service.ReplaceUploadPlaceholders(data, context);

        var stored = JsonSerializer.Deserialize<List<UploadDto>>(data["upload"].ToString()!);
        Assert.Equal("progress.pdf", stored![0].OriginalFileName);
    }

    [Fact]
    public void ReplaceUploadPlaceholders_uses_accumulated_collection_item_when_progress_is_empty()
    {
        var file = new UploadDto { Id = Guid.NewGuid(), OriginalFileName = "db.pdf" };
        var items = JsonSerializer.Serialize(new[]
        {
            new Dictionary<string, object>
            {
                ["id"] = "item-1",
                ["upload"] = JsonSerializer.Serialize(new[] { file })
            }
        });
        _responses.GetAccumulatedFormData().Returns(new Dictionary<string, object> { ["members"] = items });
        var data = new Dictionary<string, object>
        {
            ["upload"] = FormEngineConstants.UploadFieldSessionPlaceholder
        };

        _service.ReplaceUploadPlaceholders(data, new FormFileFieldContext(_applicationId, "flow-1", "item-1"));

        var stored = JsonSerializer.Deserialize<List<UploadDto>>(data["upload"].ToString()!);
        Assert.Equal("db.pdf", stored![0].OriginalFileName);
    }

    [Fact]
    public void ContainsFileName_returns_false_for_blacklisted_names()
    {
        _infectedFileStore.IsFileNameInfected(_applicationId.ToString(), "dup.pdf").Returns(true);

        var result = _service.ContainsFileName(
            new FormFileFieldContext(_applicationId, null, null),
            "evidence",
            "dup.pdf");

        Assert.False(result);
    }

    [Fact]
    public void ContainsFileName_matches_existing_session_file()
    {
        var files = new List<UploadDto> { new() { Id = Guid.NewGuid(), OriginalFileName = "notes.pdf" } };
        _session.SetString(FormSessionKeys.UploadedFiles(_applicationId, "evidence"), JsonSerializer.Serialize(files));

        var result = _service.ContainsFileName(
            new FormFileFieldContext(_applicationId, null, null),
            "evidence",
            "NOTES.pdf");

        Assert.True(result);
    }

    [Fact]
    public void SaveFiles_ignores_regular_uploads_without_an_application_id()
    {
        _service.SaveFiles(new FormFileFieldContext(null, null, null), "evidence", [new UploadDto()]);
        Assert.Empty(_session.Keys);
    }
}
