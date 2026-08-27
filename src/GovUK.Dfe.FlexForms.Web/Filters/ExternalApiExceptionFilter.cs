using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Web.Telemetry;
using GovUK.Dfe.FlexForms.Application.Exceptions;
using GovUK.Dfe.FlexForms.Web.Constants;
using GovUK.Dfe.FlexForms.Web.Pages.FormEngine;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Web.Interfaces;
using GovUK.Dfe.FlexForms.Web.Tenancy;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Web.Filters
{
    public class ExternalApiPageExceptionFilter(ILogger<ExternalApiPageExceptionFilter> logger) : IAsyncPageFilter
    {
        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
            => Task.CompletedTask;

        public async Task OnPageHandlerExecutionAsync(
            PageHandlerExecutingContext context,
            PageHandlerExecutionDelegate next)
        {
            var uploadInfo = DetectUploadRequest(context);
            if (uploadInfo.isUpload)
            {
                context.HttpContext.Items["UploadRequestInfo"] = uploadInfo;
            }

            var fileOpInfo = DetectFileOperationRequest(context);
            if (fileOpInfo.Item1)
            {
                context.HttpContext.Items["FileOperationInfo"] = fileOpInfo;
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

                LogApiException(context.HttpContext, response, statusCode, message);

                if (TryHandleApplicationFileAccessDenied(context.HttpContext, page, statusCode, message, out var fileAccessResult))
                {
                    executedContext.Result = fileAccessResult;
                    executedContext.ExceptionHandled = true;
                    return;
                }

                if (TryHandleFileValidationError(context.HttpContext, page, statusCode, message, out var fileValidationResult))
                {
                    executedContext.Result = fileValidationResult;
                    executedContext.ExceptionHandled = true;
                    return;
                }

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

                if (r.StatusCode is 400 or 422)
                {
                    if (TryAddModelStateErrorsFromContext(page, r))
                    {
                        if (page is BaseFormEngineModel formEnginePage)
                            formEnginePage.EnsureFormStateForErrorDisplay();

                        executedContext.Result = new PageResult();
                        executedContext.ExceptionHandled = true;
                        return;
                    }
                }

                if (r.StatusCode == 400 || r.StatusCode == 409)
                {
                    AddNonFieldError(page, r.Message);

                    if (page is BaseFormEngineModel formPageForValidation)
                        formPageForValidation.EnsureFormStateForErrorDisplay();

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
                    page.TempData["ApiErrorId"] = r.ErrorId;
                    executedContext.Result = new RedirectToPageResult("/Error/Forbidden");
                    executedContext.ExceptionHandled = true;
                    return;
                }

                if (r.StatusCode == 403)
                {
                    page.TempData["ApiErrorId"] = r.ErrorId;
                    if (!string.IsNullOrWhiteSpace(r.Message))
                        page.TempData["AccessDeniedReason"] = r.Message;

                    if (IsApplicationRequest(context.HttpContext.Request.Path)
                        && !IsMutatingHttpMethod(context.HttpContext.Request))
                    {
                        executedContext.Result = new RedirectToPageResult("/Error/NotFound");
                        executedContext.ExceptionHandled = true;
                        return;
                    }

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

                if (r.StatusCode >= 500)
                {
                    page.TempData["ApiErrorId"] = r.ErrorId;
                    executedContext.Result = new RedirectToPageResult("/Error/ServerError");
                    executedContext.ExceptionHandled = true;
                    return;
                }
                
                page.TempData["ApiErrorId"] = r.ErrorId;
                page.TempData["ErrorMessage"] = r.Message;
                executedContext.Result = new RedirectToPageResult("/Error/General");
                executedContext.ExceptionHandled = true;
            }
        }

        private void LogApiException(
            HttpContext httpContext,
            ExceptionResponse? response,
            int statusCode,
            string? message)
        {
            var correlationId = httpContext.Request.Headers.TryGetValue(CorrelationIdForwardingHandler.HeaderName, out var headerValue)
                ? headerValue.ToString()
                : response?.CorrelationId;

            var tenantContext = httpContext.RequestServices.GetService<ITenantRequestContext>();
            var tenantId = response?.TenantId ?? tenantContext?.TenantId?.ToString();
            var userEmail = response?.UserEmail
                ?? httpContext.User.FindFirstValue(ClaimTypes.Email)
                ?? httpContext.User.Identity?.Name;
            var templateId = httpContext.Session.GetString("TemplateId");
            if (response?.Context is not null
                && response.Context.TryGetValue(FlexFormsLogContextKeys.TemplateId, out var contextTemplate)
                && contextTemplate is not null)
            {
                templateId = contextTemplate.ToString();
            }
            var path = httpContext.Request.Path.Value;
            var logLevel = statusCode >= 500 ? LogLevel.Error : LogLevel.Warning;

            logger.Log(
                logLevel,
                "External API error. StatusCode={StatusCode} ErrorId={ErrorId} CorrelationId={CorrelationId} TenantId={TenantId} UserEmail={UserEmail} TemplateId={TemplateId} Path={Path} Message={Message}",
                statusCode,
                response?.ErrorId,
                correlationId,
                tenantId,
                userEmail,
                templateId,
                path,
                message);
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

            // Telemetry Context often has CorrelationId/TenantId without field errors —
            // do not treat a bare Message as "structured validation" or we skip FormErrorStore.
            return false;
        }
        
        private static (bool isUpload, string fieldId) DetectUploadRequest(PageHandlerExecutingContext context)
        {
            var fileOp = DetectFileOperationRequest(context);
            if (fileOp.Item1 && fileOp.Item2 == "upload")
            {
                return (true, fileOp.Item3);
            }

            return (false, string.Empty);
        }

        private static bool IsFileOperation(HttpContext httpContext) =>
            httpContext.Items.TryGetValue("FileOperationInfo", out var storedInfo)
            && storedInfo is ValueTuple<bool, string, string> info
            && info.Item1;

        private static string? ResolveHandlerName(PageHandlerExecutingContext context)
        {
            var name = context.HandlerMethod?.Name;
            if (!string.IsNullOrWhiteSpace(name))
                return name;

            if (context.HttpContext.Request.Query.TryGetValue("handler", out var queryHandler)
                && !string.IsNullOrWhiteSpace(queryHandler))
                return queryHandler.ToString();

            if (context.HttpContext.Request.HasFormContentType
                && context.HttpContext.Request.Form.TryGetValue("handler", out var formHandler)
                && !string.IsNullOrWhiteSpace(formHandler))
                return formHandler.ToString();

            return null;
        }

        private static string NormalizeHandlerName(string? handlerName)
        {
            if (string.IsNullOrWhiteSpace(handlerName))
                return string.Empty;

            var name = handlerName.Trim();
            if (name.StartsWith("OnPost", StringComparison.OrdinalIgnoreCase))
                name = name["OnPost".Length..];
            else if (name.StartsWith("OnGet", StringComparison.OrdinalIgnoreCase))
                name = name["OnGet".Length..];

            if (name.EndsWith("Async", StringComparison.OrdinalIgnoreCase))
                name = name[..^"Async".Length];

            return name;
        }

        private static (bool isFileOp, string operation, string fieldId) DetectFileOperationRequest(PageHandlerExecutingContext context)
        {
            var handlerName = NormalizeHandlerName(ResolveHandlerName(context));
            var operation = handlerName switch
            {
                "UploadFile" => "upload",
                "DownloadFile" => "download",
                "DeleteFile" => "delete",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(operation))
            {
                return (false, string.Empty, string.Empty);
            }

            var fieldId = string.Empty;
            if (context.HttpContext.Request.HasFormContentType)
            {
                fieldId = context.HttpContext.Request.Form["FieldId"].ToString();
            }

            return (true, operation, fieldId);
        }

        private static bool TryHandleFileValidationError(
            HttpContext httpContext,
            PageModel page,
            int statusCode,
            string? apiMessage,
            out IActionResult result)
        {
            result = new PageResult();

            if (statusCode is not (400 or 409 or 422))
                return false;

            if (!IsFileOperation(httpContext))
                return false;

            var fileOpInfo = httpContext.Items.TryGetValue("FileOperationInfo", out var storedInfo)
                ? (ValueTuple<bool, string, string>)storedInfo!
                : (false, string.Empty, string.Empty);

            // Download validation errors are rare; keep existing unhandled mapping for those.
            if (fileOpInfo.Item2 is not ("upload" or "delete"))
                return false;

            AddNonFieldError(page, apiMessage);

            var returnUrl = httpContext.Request.HasFormContentType
                ? httpContext.Request.Form["ReturnUrl"].ToString()
                : string.Empty;
            if (string.IsNullOrEmpty(returnUrl))
                returnUrl = httpContext.Request.Headers.Referer.ToString();

            var errorKey = !string.IsNullOrEmpty(fileOpInfo.Item3) ? fileOpInfo.Item3 : "Error";
            try
            {
                var formErrorStore = httpContext.RequestServices.GetService<IFormErrorStore>();
                formErrorStore?.Save(errorKey, page.ModelState);
                if (!string.IsNullOrEmpty(returnUrl))
                {
                    result = new RedirectResult(returnUrl);
                    return true;
                }
            }
            catch
            {
                // Fall through to page result with form state rehydrated when possible.
            }

            if (page is BaseFormEngineModel formEnginePage)
                formEnginePage.EnsureFormStateForErrorDisplay();

            result = new PageResult();
            return true;
        }

        private static bool TryHandleApplicationFileAccessDenied(
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

            var fileOpInfo = httpContext.Items.TryGetValue("FileOperationInfo", out var storedInfo)
                ? (ValueTuple<bool, string, string>)storedInfo!
                : (false, string.Empty, string.Empty);

            if (!fileOpInfo.Item1)
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

            var message = fileOpInfo.Item2 switch
            {
                "upload" => ApplicationAccessMessages.NoFileWritePermission,
                "download" => ApplicationAccessMessages.NoFileReadPermission,
                "delete" => ApplicationAccessMessages.NoFileDeletePermission,
                _ => ApplicationAccessMessages.NoAccess
            };

            AddNonFieldError(page, message);

            if (fileOpInfo.Item2 is "upload" or "delete")
            {
                var returnUrl = httpContext.Request.HasFormContentType
                    ? httpContext.Request.Form["ReturnUrl"].ToString()
                    : string.Empty;
                if (string.IsNullOrEmpty(returnUrl))
                    returnUrl = httpContext.Request.Headers.Referer.ToString();

                var errorKey = !string.IsNullOrEmpty(fileOpInfo.Item3) ? fileOpInfo.Item3 : "Error";
                try
                {
                    var formErrorStore = httpContext.RequestServices.GetService<IFormErrorStore>();
                    formErrorStore?.Save(errorKey, page.ModelState);
                    if (!string.IsNullOrEmpty(returnUrl))
                    {
                        result = new RedirectResult(returnUrl);
                        return true;
                    }
                }
                catch (Exception)
                {
                    // Fall through to page result
                }
            }

            if (fileOpInfo.Item2 == "download")
            {
                page.TempData["AccessDeniedReason"] = message;
                result = new RedirectToPageResult("/Error/Forbidden");
                return true;
            }

            result = new PageResult();
            return true;
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

            if (IsFileOperation(httpContext))
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
