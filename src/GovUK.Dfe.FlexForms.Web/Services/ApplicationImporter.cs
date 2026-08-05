using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Interfaces;
using System.Diagnostics;

namespace GovUK.Dfe.FlexForms.Web.Services;

public class ApplicationImporter(ITemplateManagementService templateManagementService) : IApplicationImporter
{
    public async Task<ApplicationImportResult> ImportSpreadsheet(Guid templateId, Stream stream, IDictionary<string, string> mapping)
    {
        Debug.WriteLine($"Importing spreadsheet for templateId: {templateId}, stream length: {stream.Length}.");

        FormTemplate template = await templateManagementService.LoadTemplateAsync(templateId.ToString());
        if (template == null)
        {
            return new ApplicationImportResult { Errors = [$"Template not found ({templateId})"] };
        }

        Dictionary<string, string?>? fields = GetSpreadsheetFields(stream, "Sheet1", mapping, out IList<string> errors);
        if (fields == null || fields.Count == 0)
        {
            return new ApplicationImportResult { Errors = [$"Failed to get spreadsheet fields: {string.Join(", ", errors)}"] };
        }

        ApplicationImport applicationImport = BuildApplicationImport(fields, template);
        if (applicationImport.Errors != null && applicationImport.Errors.Any())
        {
            return new ApplicationImportResult { Errors = applicationImport.Errors };
        }

        Dictionary<string, object> data = [];
        foreach (var field in fields)
        {
            data.Add(field.Key, field.Value ?? string.Empty);
        }

        return new ApplicationImportResult { Success = true, Template = template, Data = data };
    }

    private static Dictionary<string, string?>? GetSpreadsheetFields(Stream stream, string sheet, IDictionary<string, string> mapping, out IList<string> errors)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(stream, false);

        WorkbookPart? wbPart = document.WorkbookPart;

        errors = [];

        Sheet? theSheet = wbPart?.Workbook?.Descendants<Sheet>().Where(s => s.Name == sheet).FirstOrDefault();
        if (theSheet is null || theSheet.Id is null)
        {
            errors.Add($"Sheet '{sheet}' not found in the spreadsheet.");
            return default;
        }

        WorksheetPart wsPart = (WorksheetPart)wbPart!.GetPartById(theSheet.Id!);

        Dictionary<string, string?> fields = [];

        foreach (var kvp in mapping)
        {
            Cell? theCell = wsPart.Worksheet?.Descendants<Cell>()?.Where(c => c.CellReference == kvp.Key).FirstOrDefault();
            if (theCell is null)
            {
                errors.Add($"Cell '{kvp.Key}' not found in the worksheet.");
                continue;
            }
            string? cellValue;
            if (theCell is null || theCell.InnerText.Length < 0)
            {
                fields.Add(kvp.Key, null);
                continue;
            }
            cellValue = theCell.InnerText;
            if (theCell.DataType is not null)
            {
                if (theCell.DataType.Value == CellValues.SharedString)
                {
                    var stringTable = wbPart.GetPartsOfType<SharedStringTablePart>().FirstOrDefault();
                    if (stringTable is not null)
                    {
                        cellValue = stringTable.SharedStringTable!.ElementAt(int.Parse(cellValue)).InnerText;
                    }
                }
                else if (theCell.DataType.Value == CellValues.Boolean)
                {
                    cellValue = cellValue switch
                    {
                        "0" => "FALSE",
                        _ => "TRUE",
                    };
                }
            }

            Debug.WriteLine($"Cell {kvp.Key}: {cellValue}");
            fields.Add(kvp.Key, cellValue);
        }

        return fields;
    }

    private static ApplicationImport BuildApplicationImport(IDictionary<string, string?> fields, FormTemplate template)
    {
        Dictionary<string, string> fieldMapping = []; // TODO get from template or external source

        List<string> warnings = [];
        List<string> errors = [];
        List<dynamic> responseFields = [];
        foreach (var field in fields)
        {
            var matchedField = template.TaskGroups
                .SelectMany(tg => tg.Tasks)
                .SelectMany(t => t.Pages!)
                .SelectMany(p => p.Fields)
                .FirstOrDefault(f => f.FieldId == field.Key);

            if (matchedField == null)
            {
                errors.Add($"No single matching field found in the template for field '{field.Key}'");
                continue;
            }

            // TODO construct the response field JSON based on the matched field and its value
            var responseField = new
            {
                FieldId = field.Key,
                Value = field.Value
            };
            responseFields.Add(responseField);
        }

        // TODO construct the response body JSON based on the matched fields and their values

        return new ApplicationImport()
        {
            Warnings = warnings,
            Errors = errors,
            ResponseBody = null
        };
    }
}

internal class ApplicationImport
{
    public string? ResponseBody { get; set; }
    public IEnumerable<string>? Warnings { get; set; }
    public IEnumerable<string>? Errors { get; set; }
}
