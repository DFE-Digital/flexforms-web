using GovUK.Dfe.FlexForms.Application.Exceptions;
using GovUK.Dfe.FlexForms.Web.Constants;
using GovUK.Dfe.FlexForms.Web.Pages.FormEngine;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using GovUK.Dfe.FlexForms.Web.Interfaces;

namespace GovUK.Dfe.FlexForms.Web.Filters
{
    public class ExternalApiPageExceptionFilter : IAsyncPageFilter
    {
        
        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
            => Task.CompletedTask;

        public async Task OnPageHandlerExecutionAsync(
            PageHandlerExecutingContext context,
            PageHandlerExecutionDelegate next)
        {
            
            // DETECT UPLOAD REQUESTS BEFORE EXECUTION
            var uploadInfo = DetectUploadRequest(context);
            if (uploadInfo.isUpload)
            {
                
                context.HttpContext.Items["UploadRequestInfo"] = uploadInfo;
            }
            
            var executedContext = await next();

            if (executedContext.Exception is ApplicationAccessException
                && !executedContext.ExceptionHandled)
            {
                executedContext.Result = new RedirectToPageResult("/Error/NotFound");
                executedContext.ExceptionHandled = true;
                return;
            }

            if (executedContext.Exception is ExternalApplicationsException apiException
                && !executedContext.ExceptionHandled)
            {
                var page = context.HandlerInstance as PageModel
                           ?? throw new InvalidOperationException("Page filter only for Razor Pages");

                var response = (apiException as ExternalApplicationsException<ExceptionResponse>)?.Result;
                var statusCode = response?.StatusCode ?? apiException.StatusCode;
                var message = response?.Message ?? apiException.Message;

                if (TryHandleApplicationWriteAccessDenied(context.HttpContext, page, statusCode, message, out var writeAccessResult))
                {
                    executedContext.Result = writeAccessResult;
                    executedContext.ExceptionHandled = true;
                    return;
                }

                if (response is null)
                {
                    executedContext.Result = MapUnhandledApiException(page, statusCode, message, context.HttpContext);
                    executedContext.ExceptionHandled = true;
                    return;
                }

                var r = response;

                // 1) Validation: attempt to map structured validation errors into ModelState
                if (r.StatusCode is 400 or 422)
                {
                    
                    if (TryAddModelStateErrorsFromContext(page, r))
                    {
                        
                        executedContext.Result = new PageResult();
                        executedContext.ExceptionHandled = true;
                        
                        return;
                    }
                }

                if (r.StatusCode == 400 || r.StatusCode == 409)
                {
                    
                    AddNonFieldError(page, r.Message);

                    // SPECIAL HANDLING FOR UPLOAD REQUESTS: Use stored upload info
                    
                    var storedUploadInfo = context.HttpContext.Items.TryGetValue("UploadRequestInfo", out var storedInfo) 
                        ? ((bool isUpload, string fieldId))storedInfo 
                        : (false, string.Empty);
                    
                    if (storedUploadInfo.Item1)
                    {
                        
                        try 
                        {
                            var formErrorStore = context.HttpContext.RequestServices.GetService<GovUK.Dfe.FlexForms.Web.Interfaces.IFormErrorStore>();
                            if (formErrorStore != null)
                            {
                                formErrorStore.Save(storedUploadInfo.Item2, page.ModelState);
                                
                                // Get the return URL from the request
                                var returnUrl = context.HttpContext.Request.Form["ReturnUrl"].ToString();
                                if (!string.IsNullOrEmpty(returnUrl))
                                {
                                    
                                    executedContext.Result = new Microsoft.AspNetCore.Mvc.RedirectResult(returnUrl);
                                    executedContext.ExceptionHandled = true;
                                    return;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            
                        }
                    }

                    
                    executedContext.Result = new PageResult();
                    executedContext.ExceptionHandled = true;
                    
                    return;
                }

                if (r.StatusCode == 429)
                {
                    page.TempData["ApiErrorId"] = r.ErrorId;
                    page.TempData["ErrorMessage"] = r.Message;
                    executedContext.Result = new RedirectToPageResult("/Error/General");
                    executedContext.ExceptionHandled = true;
                    return;
                }

                if (r.StatusCode == 404)
                {
                    executedContext.Result = new RedirectToPageResult("/Error/NotFound");
                    executedContext.ExceptionHandled = true;
                    return;
                }

                if (r.StatusCode == 401)
                {
                    var logger = context.HttpContext.RequestServices.GetService<ILogger<ExternalApiPageExceptionFilter>>();
                    var userId = context.HttpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";

                    page.TempData["ApiErrorId"] = r.ErrorId;
                    executedContext.Result = new RedirectToPageResult("/Error/Forbidden");
                    executedContext.ExceptionHandled = true;
                    return;
                }

                if (r.StatusCode == 403)
                {
                    var logger = context.HttpContext.RequestServices.GetService<ILogger<ExternalApiPageExceptionFilter>>();
                    var userId = context.HttpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";

                    page.TempData["ApiErrorId"] = r.ErrorId;

                    // Treat forbidden application view access the same as not found
                    if (IsApplicationRequest(context.HttpContext.Request.Path)
                        && !IsMutatingHttpMethod(context.HttpContext.Request))
                    {
                        executedContext.Result = new RedirectToPageResult("/Error/NotFound");
                        executedContext.ExceptionHandled = true;
                        return;
                    }

                    // Check if this is likely a token issue and redirect to logout
                    if (IsAuthenticationFailureMessage(r.Message))
                    {
                        executedContext.Result = new RedirectToPageResult("/Logout", new { reason = "token_expired" });
                    }
                    else
                    {
                        executedContext.Result = new RedirectToPageResult("/Error/Forbidden");
                    }

                    executedContext.ExceptionHandled = true;
                    return;
                }

                // Handle 5xx errors (server errors)
                if (r.StatusCode >= 500)
                {
                    page.TempData["ApiErrorId"] = r.ErrorId;
                    executedContext.Result = new RedirectToPageResult("/Error/ServerError");
                    executedContext.ExceptionHandled = true;
                    return;
                }
                
                // All other errors go to General error page
                page.TempData["ApiErrorId"] = r.ErrorId;
                page.TempData["ErrorMessage"] = r.Message;
                executedContext.Result = new RedirectToPageResult("/Error/General");
                executedContext.ExceptionHandled = true;
            }
            
        }

        private static void AddNonFieldError(PageModel page, string message)
        {
            if (!string.IsNullOrWhiteSpace(message))
            {
                page.ModelState.AddModelError("Error", message);
            }
        }

        private static bool TryAddModelStateErrorsFromContext(PageModel page, ExceptionResponse r)
        {
            if (r.Context is null || r.Context.Count == 0)
                return false;

            // Common keys that might hold validation dictionaries
            var possibleKeys = new[] { "validationErrors", "errors", "fieldErrors", "modelState" };
            foreach (var key in possibleKeys)
            {
                if (!r.Context.TryGetValue(key, out var value))
                    continue;

                if (value is JsonElement element)
                {
                    if (element.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in element.EnumerateObject())
                        {
                            // Accept arrays or single string
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var msg in prop.Value.EnumerateArray().Select(v => v.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)))
                                {
                                    page.ModelState.AddModelError(prop.Name, msg!);
                                }
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.String)
                            {
                                var msg = prop.Value.GetString();
                                if (!string.IsNullOrWhiteSpace(msg))
                                    page.ModelState.AddModelError(prop.Name, msg!);
                            }
                        }
                        return true;
                    }
                }
            }

            // Fallback: add high-level message/details if present
            if (!string.IsNullOrWhiteSpace(r.Message))
            {
                page.ModelState.AddModelError("Error", r.Message);
                return true;
            }

            return false;
        }
        
        private static (bool isUpload, string fieldId) DetectUploadRequest(PageHandlerExecutingContext context)
        {
            // Check if this is an upload handler
            var handlerName = context.HandlerMethod?.Name;
            
            // Handle both possible handler name formats
            var isUploadHandler = handlerName == "OnPostUploadFileAsync" || handlerName == "UploadFile";
            if (!isUploadHandler)
            {
                
                return (false, string.Empty);
            }
            
            
            
            // Try to get FieldId from form data
            if (context.HttpContext.Request.HasFormContentType)
            {
                var fieldId = context.HttpContext.Request.Form["FieldId"].ToString();
                if (!string.IsNullOrEmpty(fieldId))
                {
                    return (true, fieldId);
                }
            }
            
            return (false, string.Empty);
        }

        private static bool TryHandleApplicationWriteAccessDenied(
            HttpContext httpContext,
            PageModel page,
            int statusCode,
            string? apiMessage,
            out IActionResult result)
        {
            result = new PageResult();

            if (statusCode is not (401 or 403))
            {
                return false;
            }

            if (!IsApplicationRequest(httpContext.Request.Path) || !IsMutatingHttpMethod(httpContext.Request))
            {
                return false;
            }

            if (statusCode == 401 && IsAuthenticationFailureMessage(apiMessage))
            {
                return false;
            }

            if (page is BaseFormEngineModel formEnginePage)
            {
                formEnginePage.EnsureFormStateForErrorDisplay();
            }

            AddNonFieldError(page, ApplicationAccessMessages.NoWritePermission);
            return true;
        }

        private static IActionResult MapUnhandledApiException(
            PageModel page,
            int statusCode,
            string? message,
            HttpContext httpContext)
        {
            if (statusCode == 404)
            {
                return new RedirectToPageResult("/Error/NotFound");
            }

            if (statusCode == 403
                && IsApplicationRequest(httpContext.Request.Path)
                && !IsMutatingHttpMethod(httpContext.Request))
            {
                return new RedirectToPageResult("/Error/NotFound");
            }

            if (statusCode == 401)
            {
                return new RedirectToPageResult("/Error/Forbidden");
            }

            if (statusCode == 403)
            {
                if (IsAuthenticationFailureMessage(message))
                {
                    return new RedirectToPageResult("/Logout", new { reason = "token_expired" });
                }

                return new RedirectToPageResult("/Error/Forbidden");
            }

            if (statusCode >= 500)
            {
                return new RedirectToPageResult("/Error/ServerError");
            }

            page.TempData["ErrorMessage"] = message;
            return new RedirectToPageResult("/Error/General");
        }

        private static bool IsMutatingHttpMethod(HttpRequest request) =>
            HttpMethods.IsPost(request.Method)
            || HttpMethods.IsPut(request.Method)
            || HttpMethods.IsPatch(request.Method)
            || HttpMethods.IsDelete(request.Method);

        private static bool IsAuthenticationFailureMessage(string? message) =>
            message?.Contains("token", StringComparison.OrdinalIgnoreCase) == true
            || message?.Contains("expired", StringComparison.OrdinalIgnoreCase) == true
            || message?.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) == true;

        private static bool IsApplicationRequest(PathString path)
        {
            if (!path.HasValue)
            {
                return false;
            }

            return path.Value!.StartsWith("/applications/", StringComparison.OrdinalIgnoreCase);
        }
    }
}
