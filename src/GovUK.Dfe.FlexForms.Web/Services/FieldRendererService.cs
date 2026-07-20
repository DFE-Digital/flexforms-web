using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using System.Diagnostics.CodeAnalysis;
using GovUK.Dfe.FlexForms.Web.Pages.FormEngine;
using TaskModel = GovUK.Dfe.FlexForms.Domain.Models.Task;

namespace GovUK.Dfe.FlexForms.Web.Services
{
    public class FieldRendererService(IServiceProvider serviceProvider) : IFieldRendererService
    {
        public async Task<IHtmlContent> RenderFieldAsync(Field field, string prefix, string currentValue, IReadOnlyCollection<string> selectedValues, string errorMessage, TaskModel currentTask, Page currentPage)
        {
            if (serviceProvider.GetRequiredService<IHtmlHelper>() is not IViewContextAware htmlHelper)
            {
                throw new InvalidOperationException("IHtmlHelper is not IViewContextAware.");
            }

            var actionContextAccessor = serviceProvider.GetRequiredService<IActionContextAccessor>();
            var tempDataProvider = serviceProvider.GetRequiredService<ITempDataProvider>();

            if (actionContextAccessor.ActionContext is null)
            {
                throw new InvalidOperationException("Cannot render field without an ActionContext");
            }

            var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
            {
                Model = new FieldViewModel(field, prefix, DisplayHelpers.UnsanitiseHtmlInput(currentValue), errorMessage, currentTask, currentPage, selectedValues)
            };

            // Pass route parameters to ViewData for use in partial views
            var routeData = actionContextAccessor.ActionContext.RouteData.Values;
            viewData["referenceNumber"] = routeData.TryGetValue("referenceNumber", out var refNum) ? refNum : null;
            viewData["taskId"] = routeData.TryGetValue("taskId", out var taskId) ? taskId : null;
            viewData["pageId"] = routeData.TryGetValue("pageId", out var pageId) ? pageId : null;

            // Also pass applicationId from session if available
            var httpContext = actionContextAccessor.ActionContext!.HttpContext;
            viewData["applicationId"] = httpContext.Session.GetString("ApplicationId");

            var tempData = new TempDataDictionary(actionContextAccessor.ActionContext.HttpContext, tempDataProvider);

            var viewContext = new ViewContext(
                actionContextAccessor.ActionContext,
                new FakeView(),
                viewData,
                tempData,
                TextWriter.Null,
                new HtmlHelperOptions());

            htmlHelper.Contextualize(viewContext);

            var partialName = field.Type switch
            {
                "text" => "Fields/_TextField",
                "email" => "Fields/_EmailField",
                "select" => "Fields/_SelectField",
                "text-area" => "Fields/_TextAreaField",
                "radios" => "Fields/_RadiosField",
                "checkboxes" => "Fields/_CheckboxesField",
                "character-count" => "Fields/_CharacterCountField",
                "date" => "Fields/_DateInputField",
                "autocomplete" => "Fields/_AutocompleteField",
                "complexField" => "Fields/_ComplexField",
                _ => throw new NotSupportedException($"Field type '{field.Type}' not supported")
            };

            viewData.Model = new FieldViewModel(field, prefix, DisplayHelpers.UnsanitiseHtmlInput(currentValue), errorMessage, currentTask, currentPage, selectedValues);

            return await ((IHtmlHelper)htmlHelper).PartialAsync($"~/Views/Shared/{partialName}.cshtml", viewData.Model);
        }
    }

    [ExcludeFromCodeCoverage]
    internal class FakeView : IView
    {
        public string Path => string.Empty;

        public System.Threading.Tasks.Task RenderAsync(ViewContext context) =>
            System.Threading.Tasks.Task.CompletedTask;
    }
}