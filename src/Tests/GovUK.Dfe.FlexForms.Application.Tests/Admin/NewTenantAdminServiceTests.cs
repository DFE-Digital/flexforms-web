using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Request;
using GovUK.Dfe.CoreLibs.Contracts.ExternalApplications.Models.Response;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Application.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Task = System.Threading.Tasks.Task;

namespace GovUK.Dfe.FlexForms.Application.Tests.Admin;

public class NewTenantAdminServiceTests
{
    private readonly ITenantAdminClient _client = Substitute.For<ITenantAdminClient>();
    private readonly NewTenantAdminService _service;
    private readonly NewTenantWorkState _state;

    public NewTenantAdminServiceTests()
    {
        _service = new NewTenantAdminService(_client, NullLogger<NewTenantAdminService>.Instance);
        _state = new NewTenantWorkState
        {
            SourceTenantId = Guid.NewGuid(),
            SourceTenantName = "Transfers",
            NewTenantId = Guid.NewGuid(),
            NewTenantName = "Transfers new",
            ServiceName = "Apply to transfer",
            Hostname = "new.example.test",
            FrontendOrigin = "https://new.example.test",
            Settings = [new NewTenantSettingDraft { Category = "Authentication", Target = "Web", SettingsJson = """{"Scheme":"DfESignIn"}""" }]
        };
    }

    [Fact]
    public async Task LoadDraftAsync_ShouldDefaultToTestAuthentication_WithGeneratedSecrets_WhenAllowed()
    {
        _state.TestAuthenticationAllowed = true;
        GivenSourceSettings(
            Row("Authentication", "Web", """{"Scheme":"DfESignIn"}"""),
            Row("DfESignIn", "Shared", """{"Authority":"https://signin.example.test","ClientId":"dsi","ClientSecret":"__REDACTED__"}"""),
            Row("Email", "Api", """{"Provider":"GovUkNotify","GovUkNotify":{"ApiKey":"__REDACTED__"}}"""),
            Row("EmailTemplates", "Api", """{"Visits":{"ApplicationSubmitted":"t1"}}"""));

        await _service.LoadDraftAsync(_state);

        Assert.Equal(NewTenantAdminService.TestAuthentication, _state.Scheme);
        Assert.Contains("\"Scheme\": \"TestAuthentication\"", _state.Settings.Single(s => s.Category == "Authentication").SettingsJson);

        var testAuth = _state.Settings.Where(s => s.Category == "TestAuthentication").ToList();
        Assert.Equal(["Api", "Web"], testAuth.Select(s => s.Target));
        Assert.All(testAuth, s => Assert.False(s.IsOptional));
        Assert.Equal(testAuth[0].SettingsJson, testAuth[1].SettingsJson);
        var testAuthJson = JsonNode.Parse(testAuth[0].SettingsJson)!;
        Assert.True(testAuthJson["Enabled"]!.GetValue<bool>());
        Assert.True(testAuthJson["JwtSigningKey"]!.GetValue<string>().Length >= 32);
        Assert.Equal(_state.NewTenantId.ToString(), testAuthJson["JwtIssuer"]!.GetValue<string>());

        var dfeSignIn = _state.Settings.Single(s => s.Category == "DfESignIn");
        Assert.False(dfeSignIn.IsOptional);
        Assert.Contains("https://signin.example.test", dfeSignIn.SettingsJson);

        Assert.False(_state.Settings.Single(s => s.Category == "Email").IsOptional);
        var templates = JsonNode.Parse(_state.Settings.Single(s => s.Category == "EmailTemplates").SettingsJson)!;
        Assert.Equal("t1", templates["Visits"]!["ApplicationSubmitted"]!.GetValue<string>());
        Assert.Equal("", templates["Visits"]!["TestAuthPasswordEmail"]!.GetValue<string>());
    }

    [Fact]
    public async Task LoadDraftAsync_ShouldPrefillServiceName_FromLayout_ThenTenantName()
    {
        _state.ServiceName = "";
        GivenSourceSettings(
            Row("Layout", "Shared", """{"ServiceName":"Shared name"}"""),
            Row("Layout", "Web", """{"ServiceName":"Visit a school","Phase":"beta"}"""));

        await _service.LoadDraftAsync(_state);

        Assert.Equal("Visit a school", _state.ServiceName);

        _state.ServiceName = "";
        GivenSourceSettings();

        await _service.LoadDraftAsync(_state);

        Assert.Equal("Transfers", _state.ServiceName);
    }

    [Fact]
    public void WithServiceName_ShouldOverwriteLayoutServiceName_AndKeepOtherKeys()
    {
        var rows = NewTenantAdminService.WithServiceName(
            [
                new NewTenantSettingDraft { Category = "Layout", Target = "Web", SettingsJson = """{"ServiceName":"Old","Phase":"beta"}""" },
                new NewTenantSettingDraft { Category = "TokenRefresh", Target = "Web", SettingsJson = " " }
            ],
            "New service");

        var layout = JsonNode.Parse(Assert.Single(rows).SettingsJson)!;
        Assert.Equal("New service", layout["ServiceName"]!.GetValue<string>());
        Assert.Equal("beta", layout["Phase"]!.GetValue<string>());
    }

    [Fact]
    public void WithServiceName_ShouldAddWebLayout_WhenNoneIsLeft()
    {
        var rows = NewTenantAdminService.WithServiceName(
            [new NewTenantSettingDraft { Category = "Layout", Target = "Web", SettingsJson = "" }],
            "New service");

        var layout = Assert.Single(rows);
        Assert.Equal(("Layout", "Web"), (layout.Category, layout.Target));
        Assert.Equal("New service", JsonNode.Parse(layout.SettingsJson)!["ServiceName"]!.GetValue<string>());
    }

    [Fact]
    public void ValidateInput_ShouldReturnError_WhenServiceNameIsBlank()
    {
        _state.ServiceName = " ";

        var errors = _service.ValidateInput(_state);

        Assert.Contains(errors, e => e.FieldKey == "ServiceName" && e.Message == NewTenantMessages.ServiceNameRequired);
    }

    [Fact]
    public async Task LoadDraftAsync_ShouldLeaveOutTestAuthentication_WhenNotAllowed_EvenForTestAuthTenant()
    {
        GivenSourceSettings(
            Row("Authentication", "Web", """{"Scheme":"TestAuthentication"}"""),
            Row("TestAuthentication", "Web", """{"Enabled":true}"""),
            Row("DfESignIn", "Shared", """{"Authority":"https://signin.example.test"}"""));

        await _service.LoadDraftAsync(_state);

        Assert.Equal(NewTenantAdminService.DfESignIn, _state.Scheme);
        Assert.DoesNotContain(_state.Settings, s => s.Category is "TestAuthentication" or "EmailTemplates");
    }

    [Fact]
    public async Task LoadDraftAsync_ShouldCopyOnlyMinimumRows_ForDfESignInTenant_WhenTestAuthenticationNotAllowed()
    {
        GivenSourceSettings(
            Row("ConnectionStrings", "Api", """{"DefaultConnection":"__REDACTED__"}"""),
            Row("Authorization", "Api", """{"TokenSettings":{"SecretKey":"__REDACTED__","Issuer":"old","Audience":"old","TokenLifetimeMinutes":30}}"""),
            Row("AzureAd", "Shared", """{"TenantId":"entra","ClientId":"api"}"""),
            Row("Authentication", "Web", """{"Scheme":"dsi"}"""),
            Row("DfESignIn", "Shared", """{"Authority":"https://signin.example.test","ClientId":"c","ClientSecret":"__REDACTED__"}"""),
            Row("EntraSso", "Shared", """{"Enabled":false}"""),
            Row("Layout", "Web", """{"ServiceName":"Transfers"}"""),
            Row("InternalServiceAuth", "Api", """{"SecretKey":"__REDACTED__"}"""),
            Row("FileStorage", "Api", """{"Provider":"Hybrid"}"""));

        await _service.LoadDraftAsync(_state);

        Assert.Equal(NewTenantAdminService.DfESignIn, _state.Scheme);
        Assert.Equal(
            ["ConnectionStrings", "Authorization", "AzureAd", "Authentication", "DfESignIn"],
            _state.Settings.Where(s => !s.IsOptional).Select(s => s.Category));
        Assert.Equal(
            ["Layout", "FileStorage", "EntraSso"],
            _state.Settings.Where(s => s.IsOptional).Select(s => s.Category));
        Assert.DoesNotContain(_state.Settings, s => s.Category == "InternalServiceAuth");
        Assert.Equal("Shared", _state.Settings.Single(s => s.Category == "AzureAd").Target);
        Assert.Contains("__REDACTED__", _state.Settings.Single(s => s.Category == "ConnectionStrings").SettingsJson);

        var tokenSettings = JsonNode.Parse(_state.Settings.Single(s => s.Category == "Authorization").SettingsJson)!["TokenSettings"]!;
        Assert.True(tokenSettings["SecretKey"]!.GetValue<string>().Length >= 32);
        Assert.Equal(_state.NewTenantId.ToString(), tokenSettings["Issuer"]!.GetValue<string>());
        Assert.Equal(30, tokenSettings["TokenLifetimeMinutes"]!.GetValue<int>());
        Assert.Contains("\"Scheme\": \"DfESignIn\"", _state.Settings.Single(s => s.Category == "Authentication").SettingsJson);
    }

    [Fact]
    public async Task LoadDraftAsync_ShouldAddTestAuthEmailAndTokenRefreshSource_ForTestAuthenticationTenant()
    {
        _state.TestAuthenticationAllowed = true;
        GivenSourceSettings(
            Row("Authentication", "Web", """{"Scheme":"TestAuthentication"}"""),
            Row("TestAuthentication", "Web", """{"Enabled":true,"JwtSigningKey":"__REDACTED__","JwtIssuer":"old","JwtAudience":"old"}"""),
            Row("Email", "Api", """{"Provider":"GovUkNotify","GovUkNotify":{"ApiKey":"__REDACTED__"}}"""),
            Row("DfESignIn", "Shared", """{"Authority":"https://signin.example.test"}"""));

        await _service.LoadDraftAsync(_state);

        Assert.Equal(NewTenantAdminService.TestAuthentication, _state.Scheme);
        var testAuth = _state.Settings.Where(s => s.Category == "TestAuthentication").ToList();
        Assert.Equal(["Api", "Web"], testAuth.Select(s => s.Target));
        Assert.Equal(testAuth[0].SettingsJson, testAuth[1].SettingsJson);
        Assert.DoesNotContain("__REDACTED__", testAuth[0].SettingsJson);
        Assert.Contains(_state.Settings, s => s.Category == "Email");
        Assert.Contains(_state.Settings, s => s.Category == "EmailTemplates" && s.SettingsJson.Contains("Transfers"));
        Assert.Contains(_state.Settings, s => s.Category == "DfESignIn");
        Assert.Contains(_state.Settings, s => s.Category == "ConnectionStrings" && s.SettingsJson.Contains("DefaultConnection"));
    }

    [Fact]
    public async Task LoadDraftAsync_ShouldCopyEveryOptionalCategory_AsIs_WithoutPlaceholders()
    {
        var optional = NewTenantAdminService.CopiedCategories
            .Where(c => c is not (NewTenantAdminService.DfESignIn))
            .Select(c => Row(c, "Shared", """{"Copied":"yes"}"""))
            .ToArray();
        GivenSourceSettings(
            [Row("DfESignIn", "Shared", """{"Authority":"https://signin.example.test"}"""), .. optional]);

        await _service.LoadDraftAsync(_state);

        var copied = _state.Settings.Where(s => s.IsOptional).ToList();
        Assert.Equal(optional.Select(o => o.Category), copied.Select(c => c.Category));
        Assert.All(copied, c => Assert.Contains("\"Copied\": \"yes\"", c.SettingsJson));
        Assert.Single(_state.Settings, s => s.Category == "DfESignIn");
    }

    [Fact]
    public async Task LoadDraftAsync_ShouldSkipOptionalCategories_TheCurrentTenantDoesNotHave()
    {
        GivenSourceSettings(Row("DfESignIn", "Shared", """{"Authority":"https://signin.example.test"}"""));

        await _service.LoadDraftAsync(_state);

        Assert.DoesNotContain(_state.Settings, s => s.IsOptional);
    }

    [Fact]
    public async Task LoadDraftAsync_ShouldAddDfESignInPlaceholder_ForEntraTenantWithoutDfESignIn()
    {
        GivenSourceSettings(Row("EntraSso", "Shared", """{"Enabled":true,"TenantId":"t","ClientId":"c"}"""));

        await _service.LoadDraftAsync(_state);

        Assert.Equal(NewTenantAdminService.EntraSso, _state.Scheme);
        var dfeSignIn = _state.Settings.Single(s => s.Category == "DfESignIn");
        Assert.False(dfeSignIn.IsOptional);
        Assert.Contains("DiscoveryEndpoint", dfeSignIn.SettingsJson);
        Assert.DoesNotContain(_state.Settings, s => s.Category == "TokenRefresh");
    }

    [Fact]
    public async Task LoadDraftAsync_ShouldFlagError_WhenSettingsCannotBeLoaded()
    {
        _client.GetTenantSettingsAsync(_state.SourceTenantId, Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 500, "err", null!, null!));

        await _service.LoadDraftAsync(_state);

        Assert.True(_state.HasError);
        Assert.Equal(NewTenantMessages.SettingsLoadFailed, _state.ErrorMessage);
        Assert.Contains(_state.Settings, s => s.Category == "ConnectionStrings");
    }

    [Fact]
    public void ValidateInput_ShouldReturnError_WhenNewTenantIdMatchesSource()
    {
        _state.NewTenantId = _state.SourceTenantId;

        var errors = _service.ValidateInput(_state);

        Assert.Contains(errors, e => e.Message == NewTenantMessages.TenantIdMustDiffer);
    }

    [Fact]
    public void ValidateInput_ShouldReturnError_WhenSettingIsInvalidJson()
    {
        _state.Settings[0].SettingsJson = "{not json";

        var errors = _service.ValidateInput(_state);

        var error = Assert.Single(errors);
        Assert.Equal("Settings[0].SettingsJson", error.FieldKey);
    }

    [Fact]
    public async Task CreateAsync_ShouldSendOnlyNonBlankSettings_InBase64Payload()
    {
        _state.Settings.Add(new NewTenantSettingDraft { Category = "TokenRefresh", Target = "Web", SettingsJson = "  " });
        CloneTenantRequest? sent = null;
        _client.CloneTenantAsync(_state.SourceTenantId, Arg.Do<CloneTenantRequest>(r => sent = r), Arg.Any<CancellationToken>())
            .Returns(new DuplicateTenantResponse(
                _state.SourceTenantId, _state.NewTenantId, "Transfers new", "new.example.test", "https://new.example.test", 1, "ok"));

        var result = await _service.CreateAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.RedirectToPage, result.Kind);
        Assert.Equal(NewTenantMessages.Created("Transfers new", _state.NewTenantId, 1, "new.example.test"), result.SuccessMessage);

        using var payload = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(sent!.PayloadJson)));
        Assert.Equal("new.example.test", payload.RootElement.GetProperty("hostname").GetString());
        var settings = payload.RootElement.GetProperty("settings");
        Assert.Equal(2, settings.GetArrayLength());
        Assert.Equal("Authentication", settings[0].GetProperty("category").GetString());
        Assert.Equal("Layout", settings[1].GetProperty("category").GetString());
        Assert.Contains("Apply to transfer", settings[1].GetProperty("settingsJson").GetString());
    }

    [Fact]
    public async Task CreateAsync_ShouldStay_WithApiMessage_WhenApiRejects()
    {
        _client.CloneTenantAsync(_state.SourceTenantId, Arg.Any<CloneTenantRequest>(), Arg.Any<CancellationToken>())
            .Throws(new ExternalApplicationsException("boom", 500, "err", null!, null!));

        var result = await _service.CreateAsync(_state);

        Assert.Equal(AdminPageOutcomeKind.StayOnPage, result.Kind);
        Assert.Equal($"{NewTenantMessages.CreateFailed} (HTTP 500)", result.ErrorMessage);
        Assert.True(_state.HasError);
    }

    private void GivenSourceSettings(params TenantSettingDto[] settings) =>
        _client.GetTenantSettingsAsync(_state.SourceTenantId, Arg.Any<CancellationToken>())
            .Returns(new GetTenantSettingsResponse(_state.SourceTenantId, "Transfers", settings));

    private static TenantSettingDto Row(string category, string target, string json) =>
        new(Guid.NewGuid(), category, target, json, true, DateTime.UtcNow);
}
