using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;

namespace GovUK.Dfe.FlexForms.Application.Interfaces;

public interface IFeedbackService
{
    Task<SubmitFeedbackResult> SubmitFeedbackAsync(UserFeedbackRequest request);
}

public abstract record SubmitFeedbackResult
{
    public sealed record Success : SubmitFeedbackResult;

    public sealed record ValidationError(IDictionary<string, string[]> Errors) : SubmitFeedbackResult;
}