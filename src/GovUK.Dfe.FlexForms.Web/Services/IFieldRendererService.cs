using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.AspNetCore.Html;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Web.Services
{
    public interface IFieldRendererService
    {
        Task<IHtmlContent> RenderFieldAsync(Field field, string prefix, string currentValue, IReadOnlyCollection<string> selectedValues, string errorMessage, TaskModel currentTask, Page currentPage);
    }
}