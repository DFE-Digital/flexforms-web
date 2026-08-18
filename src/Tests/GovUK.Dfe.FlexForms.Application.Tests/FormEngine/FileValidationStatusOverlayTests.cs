using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Enums;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Application.FormEngine;

namespace GovUK.Dfe.FlexForms.Application.Tests.FormEngine;

public class FileValidationStatusOverlayTests
{
    [Fact]
    public void ApplyToFiles_copies_validation_fields_from_latest_database_row()
    {
        var fileId = Guid.NewGuid();
        var sessionFile = new UploadDto
        {
            Id = fileId,
            OriginalFileName = "scan.xlsx",
            ValidationStatus = FileValidationStatus.Pending
        };
        var dbFile = new UploadDto
        {
            Id = fileId,
            OriginalFileName = "scan.xlsx",
            ValidationStatus = FileValidationStatus.Passed,
            ValidationMessage = "OK",
            ValidatedOn = DateTime.UtcNow
        };

        FileValidationStatusOverlay.ApplyToFiles([sessionFile], FileValidationStatusOverlay.IndexById([dbFile]));

        Assert.Equal(FileValidationStatus.Passed, sessionFile.ValidationStatus);
        Assert.Equal("OK", sessionFile.ValidationMessage);
        Assert.Equal(dbFile.ValidatedOn, sessionFile.ValidatedOn);
    }

    [Fact]
    public void ApplyToFormData_updates_serialized_upload_lists_and_collection_items()
    {
        var fileId = Guid.NewGuid();
        var pending = JsonSerializer.Serialize(new List<UploadDto>
        {
            new()
            {
                Id = fileId,
                OriginalFileName = "scan.xlsx",
                ValidationStatus = FileValidationStatus.Pending
            }
        });
        var collection = JsonSerializer.Serialize(new List<Dictionary<string, object>>
        {
            new()
            {
                ["id"] = "item-1",
                ["evidence"] = pending
            }
        });
        var data = new Dictionary<string, object>
        {
            ["upload"] = pending,
            ["members"] = collection
        };
        var latest = FileValidationStatusOverlay.IndexById(
        [
            new UploadDto
            {
                Id = fileId,
                OriginalFileName = "scan.xlsx",
                ValidationStatus = FileValidationStatus.Failed,
                ValidationMessage = "Missing column"
            }
        ]);

        FileValidationStatusOverlay.ApplyToFormData(data, latest);

        var uploads = JsonSerializer.Deserialize<List<UploadDto>>(data["upload"].ToString()!);
        Assert.Equal(FileValidationStatus.Failed, uploads![0].ValidationStatus);
        Assert.Equal("Missing column", uploads[0].ValidationMessage);

        var items = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(data["members"].ToString()!);
        var nested = JsonSerializer.Deserialize<List<UploadDto>>(items![0]["evidence"].GetString()!);
        Assert.Equal(FileValidationStatus.Failed, nested![0].ValidationStatus);
    }
}
