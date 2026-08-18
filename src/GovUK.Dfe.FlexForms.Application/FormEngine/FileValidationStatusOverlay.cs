using System.Text.Json;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;

namespace GovUK.Dfe.FlexForms.Application.FormEngine;

/// <summary>
/// Copies the latest file-validation fields from the API onto session/form-data upload lists.
/// Session snapshots are taken at upload time and otherwise keep showing Pending.
/// </summary>
public static class FileValidationStatusOverlay
{
    public static void ApplyToFiles(
        IReadOnlyList<UploadDto> files,
        IReadOnlyDictionary<Guid, UploadDto> latestById)
    {
        foreach (var file in files)
        {
            if (!latestById.TryGetValue(file.Id, out var latest))
                continue;

            file.ValidationStatus = latest.ValidationStatus;
            file.ValidationMessage = latest.ValidationMessage;
            file.ValidatedOn = latest.ValidatedOn;
        }
    }

    public static IReadOnlyDictionary<Guid, UploadDto> IndexById(IEnumerable<UploadDto>? files) =>
        (files ?? [])
            .Where(file => file.Id != Guid.Empty)
            .GroupBy(file => file.Id)
            .ToDictionary(group => group.Key, group => group.First());

    public static void ApplyToFormData(
        Dictionary<string, object> data,
        IReadOnlyDictionary<Guid, UploadDto> latestById)
    {
        if (latestById.Count == 0)
            return;

        foreach (var key in data.Keys.ToList())
        {
            var updated = ApplyToValue(data[key], latestById);
            if (updated is not null)
                data[key] = updated;
        }
    }

    private static object? ApplyToValue(object? value, IReadOnlyDictionary<Guid, UploadDto> latestById)
    {
        if (value is null)
            return null;

        if (value is List<UploadDto> uploadList)
        {
            ApplyToFiles(uploadList, latestById);
            return uploadList;
        }

        if (value is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Array)
                return ApplyToJsonArray(element.GetRawText(), latestById) ?? value;

            if (element.ValueKind == JsonValueKind.String)
                return ApplyToJsonText(element.GetString(), latestById) ?? value;

            return value;
        }

        return ApplyToJsonText(value.ToString(), latestById) ?? value;
    }

    private static object? ApplyToJsonText(string? json, IReadOnlyDictionary<Guid, UploadDto> latestById)
    {
        if (string.IsNullOrWhiteSpace(json) || json[0] != '[')
            return null;

        if (TryParseUploads(json, out var files) && files.Count > 0)
        {
            ApplyToFiles(files, latestById);
            return JsonSerializer.Serialize(files);
        }

        return ApplyToCollectionItems(json, latestById);
    }

    private static object? ApplyToJsonArray(string json, IReadOnlyDictionary<Guid, UploadDto> latestById)
    {
        if (TryParseUploads(json, out var files) && files.Count > 0)
        {
            ApplyToFiles(files, latestById);
            return JsonSerializer.Serialize(files);
        }

        return ApplyToCollectionItems(json, latestById);
    }

    private static string? ApplyToCollectionItems(string json, IReadOnlyDictionary<Guid, UploadDto> latestById)
    {
        List<Dictionary<string, object>>? items;
        try
        {
            items = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(json);
        }
        catch (JsonException)
        {
            return null;
        }

        if (items is null || items.Count == 0 || items.All(item => !item.ContainsKey("id")))
            return null;

        var changed = false;
        foreach (var item in items)
        {
            foreach (var key in item.Keys.ToList())
            {
                if (key == "id")
                    continue;

                var updated = ApplyToValue(item[key], latestById);
                if (updated is null || ReferenceEquals(updated, item[key]))
                    continue;

                item[key] = updated;
                changed = true;
            }
        }

        return changed ? JsonSerializer.Serialize(items) : json;
    }

    private static bool TryParseUploads(string json, out List<UploadDto> files)
    {
        files = [];
        try
        {
            files = JsonSerializer.Deserialize<List<UploadDto>>(json) ?? [];
        }
        catch (JsonException)
        {
            return false;
        }

        return files.Count > 0
               && files.Any(file => file.Id != Guid.Empty && !string.IsNullOrWhiteSpace(file.OriginalFileName));
    }
}
