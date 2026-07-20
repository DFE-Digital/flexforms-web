using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

namespace GovUK.Dfe.FlexForms.Web.Filters
{
    public sealed class ExternalApiMvcExceptionFilter(ILogger<ExternalApiMvcExceptionFilter> logger) : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            if (context.Exception is not ExternalApplicationsException<ExceptionResponse> ex)
            {
                return;
            }

            var r = ex.Result;

            logger.LogWarning("API exception for MVC action. StatusCode: {StatusCode}, ErrorId: {ErrorId}, ExceptionType: {ExceptionType}, Message: {Message}",
                r.StatusCode, r.ErrorId, r.ExceptionType, r.Message);

            if (r.StatusCode is 401)
            {

                    
                context.Result = new UnauthorizedResult();
                context.ExceptionHandled = true;
                return;
            }
            if (r.StatusCode is 403)
            {
                var userId = context.HttpContext.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "Anonymous";
                var userClaims = string.Join(", ", context.HttpContext.User?.Claims?.Select(c => $"{c.Type}:{c.Value}") ?? Array.Empty<string>());
                

                    
                context.Result = new ForbidResult();
                context.ExceptionHandled = true;
                return;
            }

            // Build ProblemDetails for client consumers
            var problem = new ProblemDetails
            {
                Title = string.IsNullOrWhiteSpace(r.Message) ? "API error" : r.Message,
                Status = r.StatusCode,
            };
            problem.Extensions["errorId"] = r.ErrorId;
            problem.Extensions["exceptionType"] = r.ExceptionType;

            if (r.StatusCode is 429)
            {
                context.Result = new ObjectResult(problem) { StatusCode = 429 };
                context.ExceptionHandled = true;
                return;
            }

            if (r.StatusCode is 400 or 409)
            {
                context.Result = new BadRequestObjectResult(problem);
                context.ExceptionHandled = true;
                return;
            }

            context.Result = new ObjectResult(problem) { StatusCode = problem.Status ?? 500 };
            context.ExceptionHandled = true;
        }
    }
}


