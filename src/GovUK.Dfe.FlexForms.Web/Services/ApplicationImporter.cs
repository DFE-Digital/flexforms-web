using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Web.Interfaces;
using System.Diagnostics;

namespace GovUK.Dfe.FlexForms.Web.Services;

public class ApplicationImporter(ITemplateManagementService templateManagementService) : IApplicationImporter
{
    public async Task<ApplicationImportResult> ImportSpreadsheet(Guid templateId, Stream stream, SpreadsheetTemplateMapping mapping)
    {
        Debug.WriteLine($"Importing spreadsheet for templateId: {templateId}, stream length: {stream.Length}.");

        FormTemplate template = await templateManagementService.LoadTemplateAsync(templateId.ToString());
        if (template == null)
        {
            return new ApplicationImportResult { Errors = [$"Template not found ({templateId})"] };
        }

        Dictionary<string, string?>? fields = GetSpreadsheetFields(stream, mapping, out IList<string> spreadsheetErrors);
        if (fields == null || fields.Count == 0)
        {
            return new ApplicationImportResult { Errors = [$"Failed to get spreadsheet fields: {string.Join(", ", spreadsheetErrors)}"] };
        }

        if (!CanImport(fields, template, out IList<string> importErrors))
        {
            return new ApplicationImportResult { Errors = importErrors };
        }

        return new ApplicationImportResult 
        { 
            Success = true, 
            Template = template, 
            Data = fields.ToDictionary(f => f.Key, f => (object)(f.Value ?? string.Empty)) 
        };
    }

    private static Dictionary<string, string?>? GetSpreadsheetFields(Stream stream, SpreadsheetTemplateMapping mapping, out IList<string> errors)
    {
        using SpreadsheetDocument document = SpreadsheetDocument.Open(stream, false);

        WorkbookPart? wbPart = document.WorkbookPart;

        errors = [];

        Sheet? theSheet = wbPart?.Workbook?.Descendants<Sheet>().Where(s => s.Name == mapping.SheetName).FirstOrDefault();
        if (theSheet is null || theSheet.Id is null)
        {
            errors.Add($"Sheet '{mapping.SheetName}' not found in the spreadsheet.");
            return default;
        }

        WorksheetPart wsPart = (WorksheetPart)wbPart!.GetPartById(theSheet.Id!);

        Dictionary<string, string?> fields = [];

        if (mapping.Maps == null || !mapping.Maps.Any())
        {
            errors.Add("No mappings found in the template.");
            return default;
        }

        foreach (var kvp in mapping.Maps)
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

    private static bool CanImport(IDictionary<string, string?> fields, FormTemplate template, out IList<string> errors)
    {
        errors = [];
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
        }
        return errors.Count == 0;
    }
}
