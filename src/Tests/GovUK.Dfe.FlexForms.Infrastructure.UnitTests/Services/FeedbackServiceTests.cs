using AutoFixture;
using AutoFixture.AutoNSubstitute;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Http.Models;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class FeedbackServiceTests
{
    private readonly IFixture _fixture;
    private readonly IUserFeedbackClient _client;
    private readonly FeedbackService _service;

    public FeedbackServiceTests()
    {
        _fixture = new Fixture().Customize(new AutoNSubstituteCustomization { ConfigureMembers = true });
        
        _client = _fixture.Create<IUserFeedbackClient>();
        _fixture.Inject(_client);
        
        _service = _fixture.Create<FeedbackService>();
    }

    [Theory]
    [InlineData(UserFeedbackType.BugReport)]
    [InlineData(UserFeedbackType.SupportRequest)]
    [InlineData(UserFeedbackType.FeedbackOrSuggestion)]
    public async Task SubmitUserFeedbackAsync_returns_successful_result_when_client_succeeds(UserFeedbackType type)
    {
        var request = CreateRequest(type);
        
        var result = await _service.SubmitFeedbackAsync(request);
        
        Assert.IsType<SubmitFeedbackResult.Success>(result);
        await _client.Received().PostAsync(request, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(UserFeedbackType.BugReport)]
    [InlineData(UserFeedbackType.SupportRequest)]
    [InlineData(UserFeedbackType.FeedbackOrSuggestion)]
    public async Task SubmitUserFeedbackAsync_returns_validation_error_result_when_client_returns_validation_response(
        UserFeedbackType type)
    {
        var request = CreateRequest(type);
        var expectedValidationErrors = new Dictionary<string, string[]> { ["Message"] = ["Message is required"] };
        var response = _fixture.Build<ExceptionResponse>()
            .With(r => r.ExceptionType, "ValidationException")
            .With(r => r.Context, new Dictionary<string, object> { ["validationErrors"] = expectedValidationErrors })
            .Create();
        var exception = new ExternalApplicationsException<ExceptionResponse>(
            _fixture.Create<string>(),
            _fixture.Create<int>(),
            _fixture.Create<string>(),
            _fixture.Create<Dictionary<string, IEnumerable<string>>>(),
            response,
            new Exception()
        );
        _client.PostAsync(request, Arg.Any<CancellationToken>())
            .Throws(exception);
        
        var result = await _service.SubmitFeedbackAsync(request);
        
        var actualError = Assert.IsType<SubmitFeedbackResult.ValidationError>(result);
        Assert.Equal(expectedValidationErrors, actualError.Errors);
        await _client.Received().PostAsync(request, Arg.Any<CancellationToken>());
    }

    private UserFeedbackRequest CreateRequest(UserFeedbackType type) =>
        type switch
        {
            UserFeedbackType.BugReport => _fixture.Create<BugReport>(),
            UserFeedbackType.SupportRequest => _fixture.Create<SupportRequest>(),
            UserFeedbackType.FeedbackOrSuggestion => _fixture.Create<FeedbackOrSuggestion>(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };
}