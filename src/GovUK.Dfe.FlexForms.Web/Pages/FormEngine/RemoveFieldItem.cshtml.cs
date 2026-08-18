using GovUK.Dfe.FlexForms.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Web.Pages.FormEngine
{
    public class RemoveFieldItemModel(
        IApplicationResponseService applicationResponseService,
        ILogger<RemoveFieldItemModel> logger)
        : PageModel
    {
        private readonly IApplicationResponseService _applicationResponseService = applicationResponseService;
        private readonly ILogger<RemoveFieldItemModel> _logger = logger;

        public async Task<IActionResult> OnPostRemoveFieldItemAsync(string referenceNumber, string taskId, string fieldId, int index)
        {
            if (string.IsNullOrWhiteSpace(fieldId) || index < 0)
            {
                return BadRequest("Field ID and valid index are required");
            }

            var acc = _applicationResponseService.GetAccumulatedFormData();
            if (acc.TryGetValue(fieldId, out var existing))
            {
                var json = existing?.ToString() ?? "[]";
                try
                {
                    var list = JsonSerializer.Deserialize<List<object>>(json) ?? new();
                    if (index >= 0 && index < list.Count)
                    {
                        list.RemoveAt(index);
                        var updated = JsonSerializer.Serialize(list);
                        _applicationResponseService.AccumulateFormData(new Dictionary<string, object> { [fieldId] = updated });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to remove field item at index {Index} for field {FieldId}", index, fieldId);
                }
            }

            var url = $"/applications/{referenceNumber}/{taskId}";
            return Redirect(url);
        }
    }
}


