using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Api.Client.Security;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

public class FeedbackService(IUserFeedbackClient client) : IFeedbackService
{
    public async Task<SubmitFeedbackResult> SubmitFeedbackAsync(UserFeedbackRequest request)
    {
        using (AuthenticationContext.UseServiceToServiceAuthScope())
        {
            try
            {
                await client.PostAsync(request);
                return new SubmitFeedbackResult.Success();
            }
            catch (ExternalApplicationsException<ExceptionResponse> e)
            {
                if (e.Result.ExceptionType == "ValidationException")
                {
                    var errors = e.Result.Context?["validationErrors"] as IDictionary<string, string[]>;
                    return new SubmitFeedbackResult.ValidationError(errors ?? new Dictionary<string, string[]>());
                }

                throw;
            }
        }
    }
}