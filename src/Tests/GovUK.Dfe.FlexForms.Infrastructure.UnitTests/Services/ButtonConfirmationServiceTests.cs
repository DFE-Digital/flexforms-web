using GovUK.Dfe.CoreLibs.Testing.Mocks.Session;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Infrastructure.UnitTests.Services;

public class ButtonConfirmationServiceTests
{
    private readonly IConfirmationDataService _dataService = Substitute.For<IConfirmationDataService>();
    private readonly IHttpContextAccessor _httpContextAccessor = Substitute.For<IHttpContextAccessor>();

    private ButtonConfirmationService CreateService(InMemorySession? session = null)
    {
        var httpContext = new DefaultHttpContext();
        if (session != null)
        {
            httpContext.Session = session;
        }

        _httpContextAccessor.HttpContext.Returns(httpContext);
        return new ButtonConfirmationService(
            _httpContextAccessor,
            _dataService,
            NullLogger<ButtonConfirmationService>.Instance);
    }

    #region CreateConfirmation

    [Fact]
    public void CreateConfirmation_stores_context_in_session_and_returns_token()
    {
        var session = new InMemorySession();
        var service = CreateService(session);
        var request = new ConfirmationRequest
        {
            OriginalPagePath = "/applications/REF-1/t1",
            OriginalHandler = "RemoveCollectionItem",
            OriginalFormData = new Dictionary<string, object> { ["itemTitle"] = "Ada" },
            DisplayFields = ["itemTitle"],
            ReturnUrl = "/applications/REF-1/t1"
        };

        var token = service.CreateConfirmation(request);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Equal(token, request.ConfirmationToken);

        var stored = session.GetString($"Confirmation_{token}");
        Assert.False(string.IsNullOrWhiteSpace(stored));

        var context = JsonSerializer.Deserialize<ConfirmationContext>(stored!);
        Assert.NotNull(context);
        Assert.Equal(token, context!.Token);
        Assert.Equal("RemoveCollectionItem", context.Request.OriginalHandler);
        Assert.True(context.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public void CreateConfirmation_throws_when_http_context_is_null()
    {
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        var service = new ButtonConfirmationService(
            _httpContextAccessor,
            _dataService,
            NullLogger<ButtonConfirmationService>.Instance);
        var request = new ConfirmationRequest { OriginalHandler = "Submit" };

        var exception = Assert.Throws<InvalidOperationException>(() => service.CreateConfirmation(request));

        Assert.Equal("Session is not available", exception.Message);
    }

    [Fact]
    public void CreateConfirmation_throws_when_session_is_not_configured()
    {
        var service = CreateService(session: null);
        var request = new ConfirmationRequest { OriginalHandler = "Submit" };

        Assert.Throws<InvalidOperationException>(() => service.CreateConfirmation(request));
    }

    #endregion

    #region GetConfirmation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void GetConfirmation_returns_null_for_blank_token(string? token)
    {
        var service = CreateService(new InMemorySession());

        Assert.Null(service.GetConfirmation(token!));
    }

    [Fact]
    public void GetConfirmation_returns_null_when_http_context_is_unavailable()
    {
        _httpContextAccessor.HttpContext.Returns((HttpContext?)null);
        var service = new ButtonConfirmationService(
            _httpContextAccessor,
            _dataService,
            NullLogger<ButtonConfirmationService>.Instance);

        Assert.Null(service.GetConfirmation("token"));
    }

    [Fact]
    public void GetConfirmation_returns_null_when_token_not_found()
    {
        var service = CreateService(new InMemorySession());

        Assert.Null(service.GetConfirmation("missing-token"));
    }

    [Fact]
    public void GetConfirmation_returns_context_when_token_is_valid()
    {
        var session = new InMemorySession();
        var service = CreateService(session);
        const string token = "valid-token";
        var expected = new ConfirmationContext
        {
            Token = token,
            Request = new ConfirmationRequest
            {
                OriginalHandler = "RemoveCollectionItem",
                OriginalFormData = new Dictionary<string, object> { ["itemTitle"] = "Ada" }
            },
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        };
        session.SetString($"Confirmation_{token}", JsonSerializer.Serialize(expected));

        var result = service.GetConfirmation(token);

        Assert.NotNull(result);
        Assert.Equal(token, result!.Token);
        Assert.Equal("RemoveCollectionItem", result.Request.OriginalHandler);
    }

    [Fact]
    public void GetConfirmation_returns_null_and_removes_expired_token()
    {
        var session = new InMemorySession();
        var service = CreateService(session);
        const string token = "expired-token";
        var expired = new ConfirmationContext
        {
            Token = token,
            Request = new ConfirmationRequest { OriginalHandler = "Submit" },
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        };
        session.SetString($"Confirmation_{token}", JsonSerializer.Serialize(expired));

        var result = service.GetConfirmation(token);

        Assert.Null(result);
        Assert.Null(session.GetString($"Confirmation_{token}"));
    }

    [Fact]
    public void GetConfirmation_returns_null_and_removes_malformed_session_payload()
    {
        var session = new InMemorySession();
        var service = CreateService(session);
        const string token = "broken-token";
        session.SetString($"Confirmation_{token}", "{not-json");

        var result = service.GetConfirmation(token);

        Assert.Null(result);
        Assert.Null(session.GetString($"Confirmation_{token}"));
    }

    #endregion

    #region ClearConfirmation / IsValidToken

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ClearConfirmation_does_nothing_for_blank_token(string? token)
    {
        var session = new InMemorySession();
        session.SetString("Confirmation_keep", "value");
        var service = CreateService(session);

        service.ClearConfirmation(token!);

        Assert.Equal("value", session.GetString("Confirmation_keep"));
    }

    [Fact]
    public void ClearConfirmation_removes_token_from_session()
    {
        var session = new InMemorySession();
        const string token = "clear-me";
        session.SetString($"Confirmation_{token}", "{}");
        var service = CreateService(session);

        service.ClearConfirmation(token);

        Assert.Null(session.GetString($"Confirmation_{token}"));
    }

    [Fact]
    public void IsValidToken_returns_true_for_valid_token()
    {
        var session = new InMemorySession();
        var service = CreateService(session);
        const string token = "valid-token";
        session.SetString($"Confirmation_{token}", JsonSerializer.Serialize(new ConfirmationContext
        {
            Token = token,
            Request = new ConfirmationRequest(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        }));

        Assert.True(service.IsValidToken(token));
    }

    [Fact]
    public void IsValidToken_returns_false_for_missing_or_expired_token()
    {
        var session = new InMemorySession();
        var service = CreateService(session);

        Assert.False(service.IsValidToken("missing"));

        const string expiredToken = "expired";
        session.SetString($"Confirmation_{expiredToken}", JsonSerializer.Serialize(new ConfirmationContext
        {
            Token = expiredToken,
            Request = new ConfirmationRequest(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
        }));

        Assert.False(service.IsValidToken(expiredToken));
    }

    #endregion

    #region PrepareDisplayModel

    [Fact]
    public void PrepareDisplayModel_returns_null_for_invalid_token()
    {
        var service = CreateService(new InMemorySession());

        Assert.Null(service.PrepareDisplayModel("missing"));
    }

    [Fact]
    public void PrepareDisplayModel_builds_display_model_and_excludes_antiforgery_token()
    {
        var session = new InMemorySession();
        var dataService = Substitute.For<IConfirmationDataService>();
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext { Session = session });
        var service = new ButtonConfirmationService(
            accessor,
            dataService,
            NullLogger<ButtonConfirmationService>.Instance);

        const string token = "display-token";
        var request = new ConfirmationRequest
        {
            OriginalPagePath = "/applications/REF-1/t1",
            OriginalHandler = "RemoveCollectionItem",
            ReturnUrl = "/applications/REF-1/t1",
            DisplayFields = ["itemTitle"],
            OriginalFormData = new Dictionary<string, object>
            {
                ["__RequestVerificationToken"] = "ignore-me",
                ["itemTitle"] = "Ada"
            }
        };

        session.SetString($"Confirmation_{token}", JsonSerializer.Serialize(new ConfirmationContext
        {
            Token = token,
            Request = request,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        }));

        dataService
            .FormatDisplayData(Arg.Any<Dictionary<string, object>>(), Arg.Any<string[]>())
            .Returns(new Dictionary<string, string> { ["Item Title"] = "Ada" });

        var model = service.PrepareDisplayModel(token);

        Assert.NotNull(model);
        Assert.Equal("Confirm your action", model!.Title);
        Assert.Equal("Select yes if you want to continue", model.RequiredMessage);
        Assert.Equal(token, model.ConfirmationToken);
        Assert.Equal("/applications/REF-1/t1?handler=RemoveCollectionItem", model.OriginalActionUrl);
        Assert.Equal("Ada", model.DisplayData["Item Title"]);
        Assert.False(model.OriginalFormData.ContainsKey("__RequestVerificationToken"));
        Assert.Equal("Ada", model.OriginalFormData["itemTitle"]);

        dataService.Received(1).FormatDisplayData(
            Arg.Is<Dictionary<string, object>>(d =>
                d.ContainsKey("itemTitle") &&
                !d.ContainsKey("__RequestVerificationToken")),
            Arg.Any<string[]>());
    }

    [Fact]
    public void PrepareDisplayModel_augments_flattened_fields_from_json_complex_value()
    {
        var session = new InMemorySession();
        var dataService = Substitute.For<IConfirmationDataService>();
        var accessor = Substitute.For<IHttpContextAccessor>();
        accessor.HttpContext.Returns(new DefaultHttpContext { Session = session });
        var service = new ButtonConfirmationService(
            accessor,
            dataService,
            NullLogger<ButtonConfirmationService>.Instance);

        const string token = "json-token";
        var request = new ConfirmationRequest
        {
            OriginalPagePath = "/page",
            OriginalHandler = "Delete",
            DisplayFields = ["trustName", "ukprn"],
            OriginalFormData = new Dictionary<string, object>
            {
                ["complexField"] =
                    "{\"name\":\"Example Trust\",\"ukprn\":\"10000001\",\"postcode\":\"SW1A1AA\"}"
            }
        };

        session.SetString($"Confirmation_{token}", JsonSerializer.Serialize(new ConfirmationContext
        {
            Token = token,
            Request = request,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        }));

        dataService
            .FormatDisplayData(Arg.Any<Dictionary<string, object>>(), Arg.Any<string[]>())
            .Returns(new Dictionary<string, string>
            {
                ["Trust Name"] = "Example Trust",
                ["UKPRN"] = "10000001"
            });

        var model = service.PrepareDisplayModel(token);

        Assert.NotNull(model);
        Assert.Equal("Example Trust", model!.OriginalFormData["trustName"]);
        Assert.Equal("10000001", model.OriginalFormData["ukprn"]);
    }

    [Fact]
    public void PrepareDisplayModel_uses_custom_title_and_required_message_when_provided()
    {
        var session = new InMemorySession();
        var service = CreateService(session);
        const string token = "custom-token";
        var request = new ConfirmationRequest
        {
            OriginalPagePath = "/page",
            OriginalHandler = "Delete",
            Title = "Delete this item?",
            RequiredMessage = "Choose yes to delete",
            OriginalFormData = new Dictionary<string, object> { ["item"] = "Ada" },
            DisplayFields = ["item"]
        };

        session.SetString($"Confirmation_{token}", JsonSerializer.Serialize(new ConfirmationContext
        {
            Token = token,
            Request = request,
            ExpiresAt = DateTime.UtcNow.AddMinutes(5)
        }));

        _dataService
            .FormatDisplayData(Arg.Any<Dictionary<string, object>>(), Arg.Any<string[]>())
            .Returns(new Dictionary<string, string> { ["Item"] = "Ada" });

        var model = service.PrepareDisplayModel(token);

        Assert.NotNull(model);
        Assert.Equal("Delete this item?", model!.Title);
        Assert.Equal("Choose yes to delete", model.RequiredMessage);
    }

    #endregion
}
