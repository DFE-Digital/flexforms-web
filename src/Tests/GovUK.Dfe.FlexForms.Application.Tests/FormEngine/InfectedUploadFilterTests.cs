using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class InfectedUploadFilterTests
{
    private readonly IInfectedFileStore _infectedFileStore = Substitute.For<IInfectedFileStore>();
    private readonly InfectedUploadFilter _filter;

    public InfectedUploadFilterTests()
    {
        _filter = new InfectedUploadFilter(_infectedFileStore, NullLogger<InfectedUploadFilter>.Instance);
    }

    [Fact]
    public void FilterList_removes_files_blacklisted_by_id_or_name()
    {
        var infectedId = Guid.NewGuid();
        var clean = new UploadDto { Id = Guid.NewGuid(), OriginalFileName = "clean.pdf" };
        var byId = new UploadDto { Id = infectedId, OriginalFileName = "virus.bin" };
        var byName = new UploadDto { Id = Guid.NewGuid(), OriginalFileName = "bad.exe" };

        _infectedFileStore.IsFileInfected(infectedId).Returns(true);
        _infectedFileStore.IsFileNameInfected("app-1", "bad.exe").Returns(true);

        var result = _filter.FilterList([clean, byId, byName], "app-1");

        Assert.Single(result);
        Assert.Equal(clean.Id, result[0].Id);
    }

    [Fact]
    public void FilterUploadDataJson_serialises_the_filtered_list()
    {
        var infectedId = Guid.NewGuid();
        var files = new List<UploadDto>
        {
            new() { Id = infectedId, OriginalFileName = "virus.bin" },
            new() { Id = Guid.NewGuid(), OriginalFileName = "ok.pdf" }
        };
        _infectedFileStore.IsFileInfected(infectedId).Returns(true);

        var json = _filter.FilterUploadDataJson(JsonSerializer.Serialize(files), "app-1");
        var result = JsonSerializer.Deserialize<List<UploadDto>>(json);

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal("ok.pdf", result[0].OriginalFileName);
    }

    [Fact]
    public void FilterList_returns_empty_when_source_is_null_or_empty()
    {
        Assert.Empty(_filter.FilterList(null, "app-1"));
        Assert.Empty(_filter.FilterList([], "app-1"));
    }

    [Fact]
    public void FilterUploadDataJson_returns_original_value_when_not_a_file_list()
    {
        const string raw = "not-json";
        Assert.Equal(raw, _filter.FilterUploadDataJson(raw, "app-1"));
        Assert.Equal(string.Empty, _filter.FilterUploadDataJson(null, "app-1"));
    }
}
