using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Web.Services;
using Microsoft.Extensions.Configuration;

namespace GovUK.Dfe.FlexForms.Web.UnitTests.Services;

public class FileValidationUiTests
{
    [Fact]
    public void ResolveMode_ShouldPreferTemplateOverrideThenDefault()
    {
        var templateId = Guid.NewGuid();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["FileValidation:DefaultMode"] = "FailOnInvalid",
            [$"FileValidation:Templates:{templateId}"] = "RequirePassed"
        }).Build();

        Assert.Equal(FileValidationMode.RequirePassed, FileValidationUi.ResolveMode(configuration, templateId.ToString()));
        Assert.Equal(FileValidationMode.FailOnInvalid, FileValidationUi.ResolveMode(configuration, Guid.NewGuid().ToString()));
        Assert.Equal(FileValidationMode.Off, FileValidationUi.ResolveMode(new ConfigurationBuilder().Build(), null));
    }

    [Fact]
    public void GetBlockingFiles_ShouldHonourMode()
    {
        var files = new[]
        {
            new UploadDto { OriginalFileName = "ok.pdf", ValidationStatus = FileValidationStatus.Passed },
            new UploadDto { OriginalFileName = "bad.pdf", ValidationStatus = FileValidationStatus.Failed },
            new UploadDto { OriginalFileName = "wait.pdf", ValidationStatus = FileValidationStatus.Pending }
        };

        Assert.Empty(FileValidationUi.GetBlockingFiles(FileValidationMode.Off, files));
        Assert.Equal(["bad.pdf"], FileValidationUi.GetBlockingFiles(FileValidationMode.FailOnInvalid, files).Select(f => f.OriginalFileName));
        Assert.Equal(["bad.pdf", "wait.pdf"], FileValidationUi.GetBlockingFiles(FileValidationMode.RequirePassed, files).Select(f => f.OriginalFileName));
    }

    [Fact]
    public void StatusText_ShouldMapKnownStatuses()
    {
        Assert.Equal("Validation pending", FileValidationUi.StatusText(FileValidationStatus.Pending));
        Assert.Equal("Validated", FileValidationUi.StatusText(FileValidationStatus.Passed));
        Assert.Equal("Validation failed", FileValidationUi.StatusText(FileValidationStatus.Failed));
    }
}
