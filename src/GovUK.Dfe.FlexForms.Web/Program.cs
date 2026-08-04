using GovUK.Dfe.CoreLibs.Security;
using GovUK.Dfe.CoreLibs.Security.Authorization;
using GovUK.Dfe.CoreLibs.Security.Configurations;
using GovUK.Dfe.CoreLibs.Security.Interfaces;
using GovUK.Dfe.CoreLibs.Security.OpenIdConnect;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using GovUK.Dfe.FlexForms.Infrastructure.Parsers;
using GovUK.Dfe.FlexForms.Infrastructure.Providers;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using GovUK.Dfe.FlexForms.Infrastructure.Stores;
using GovUK.Dfe.FlexForms.Web.Authentication;
using GovUK.Dfe.FlexForms.Web.Extensions;
using GovUK.Dfe.FlexForms.Web.Filters;
using GovUK.Dfe.FlexForms.Web.Middleware;
using GovUK.Dfe.FlexForms.Web.Security;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUk.Frontend.AspNetCore;
using GovUK.Dfe.FlexForms.Api.Client.Extensions;
using GovUK.Dfe.FlexForms.Api.Client.Security;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.ResponseCompression;
using System.Diagnostics.CodeAnalysis;
using GovUK.Dfe.CoreLibs.Security.EntraSso;
using GovUK.Dfe.CoreLibs.Security.TokenRefresh.Extensions;
using System.IO.Compression;
using GovUK.Dfe.FlexForms.Infrastructure.Consumers;
using GovUK.Dfe.CoreLibs.Messaging.Contracts.Entities.Topics;
using GovUK.Dfe.CoreLibs.Messaging.Contracts.Messages.Events;
using GovUK.Dfe.CoreLibs.Messaging.MassTransit.Extensions;
using Microsoft.AspNetCore.Authentication;
using MassTransit;
using GovUK.Dfe.CoreLibs.Messaging.Contracts.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using GovUK.Dfe.FlexForms.Web.Telemetry;
using GovUK.Dfe.FlexForms.Web.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddJsonFile("appsettings.bootstrap.json", optional: true, reloadOnChange: true);

var platformBootstrap = builder.Configuration
    .GetSection(PlatformBootstrapOptions.SectionName)
    .Get<PlatformBootstrapOptions>();
var platformBootstrapEnabled = platformBootstrap?.Enabled ?? false;

// Path 3: tenant settings come from TenantConfig (API) via platform bootstrap.
// Legacy configurations/{APPLICATION_NAME}/ folders are not loaded at runtime.
if (!platformBootstrapEnabled)
{
    throw new InvalidOperationException(
        "PlatformBootstrap:Enabled must be true. Import tenant settings into TenantConfig and enable platform bootstrap.");
}

Console.WriteLine("[Configuration] Platform bootstrap enabled - tenant settings load from TenantConfig API.");

// Flatten local host infrastructure secrets in Development/Local (Service Bus, Redis, etc.).
// Prefer LOCAL_HOST_SECRETS_SECTION, then Platform, then Transfers (legacy user-secrets section name).
if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Local"))
{
    builder.Configuration.AddUserSecrets(typeof(Program).Assembly, optional: true);
    ApplyHostInfrastructureUserSecrets(builder.Configuration);
}

static void ApplyHostInfrastructureUserSecrets(ConfigurationManager config)
{
    var sectionCandidates = new[]
    {
        Environment.GetEnvironmentVariable("LOCAL_HOST_SECRETS_SECTION"),
        "Platform",
        "Transfers"
    }.Where(static s => !string.IsNullOrWhiteSpace(s))
     .Distinct(StringComparer.OrdinalIgnoreCase);

    foreach (var sectionName in sectionCandidates)
    {
        if (TryFlattenUserSecretsSection(config, sectionName!))
        {
            return;
        }
    }

    Console.WriteLine("[Configuration] No Platform/host user-secrets section found for local infrastructure.");
}

static bool TryFlattenUserSecretsSection(ConfigurationManager config, string sectionName)
{
    var appSecretsSection = config.GetSection(sectionName);
    if (!appSecretsSection.Exists() || !appSecretsSection.GetChildren().Any())
    {
        return false;
    }

    foreach (var secret in appSecretsSection.GetChildren())
    {
        config[secret.Key] = secret.Value;

        foreach (var child in secret.GetChildren())
        {
            BindNestedConfiguration(config, secret.Key, child);
        }
    }

    Console.WriteLine($"[Configuration] Host infrastructure user secrets loaded from section: {sectionName}");
    return true;
}

// Helper method to bind nested configuration sections
static void BindNestedConfiguration(ConfigurationManager config, string parentKey, IConfigurationSection section)
{
    var fullKey = $"{parentKey}:{section.Key}";
    if (section.Value != null)
    {
        config[fullKey] = section.Value;
    }
    foreach (var child in section.GetChildren())
    {
        BindNestedConfiguration(config, fullKey, child);
    }
}

// Environment variables always override JSON configuration
builder.Configuration.AddEnvironmentVariables();

ConfigurationManager configuration = builder.Configuration;

builder.Services.AddPlatformTenantConfiguration(configuration);
await builder.BootstrapPlatformHostConfigurationAsync();

// Reverse proxies (Azure Container Apps, Front Door) forward original scheme/host; without this,
// Request.Scheme/Host reflect the internal hop and OIDC redirect URIs do not match DfE Sign-In registration.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
        | ForwardedHeaders.XForwardedProto
        | ForwardedHeaders.XForwardedHost;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddApplicationInsightsTelemetry(configuration);

// Filter out health check endpoints from Application Insights telemetry
builder.Services.AddApplicationInsightsTelemetryProcessor<HealthCheckTelemetryFilter>();
// Configure test authentication options
builder.Services.Configure<TestAuthenticationOptions>(
    configuration.GetSection(TestAuthenticationOptions.SectionName));

// Check if test authentication is enabled
var testAuthOptions = configuration.GetSection(TestAuthenticationOptions.SectionName).Get<TestAuthenticationOptions>();
var isTestAuthEnabled = testAuthOptions?.Enabled ?? false;

// Configure Entra SSO options
builder.Services.Configure<EntraSsoOptions>(
    configuration.GetSection(EntraSsoOptions.SectionName));
var entraSsoOptions = configuration.GetSection(EntraSsoOptions.SectionName).Get<EntraSsoOptions>();
var isEntraSsoEnabled = entraSsoOptions?.Enabled ?? false;

// Configure token settings for test authentication
// This is needed when test auth is enabled
if ((isTestAuthEnabled) && testAuthOptions != null)
{
    builder.Services.Configure<GovUK.Dfe.CoreLibs.Security.Configurations.TokenSettings>(options =>
    {
        options.SecretKey = testAuthOptions.JwtSigningKey;
        options.Issuer = testAuthOptions.JwtIssuer;
        options.Audience = testAuthOptions.JwtAudience;
        options.TokenLifetimeMinutes = 60; // 1 hour default
    });
}

builder.Services.AddUserTokenServiceFactory(
    builder.Configuration,
    new Dictionary<string, string>
    {
        { "InternalService", "InternalServiceAuth" },
    });

// Add services to the container.
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.ValueLengthLimit = 4_194_304; // 4MB limit for form values
    options.ValueCountLimit = 1000;
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.ConfigureFilter(new ExternalApiPageExceptionFilter());

    options.Conventions.AuthorizeFolder("/", "OpenIdConnectPolicy");
    options.Conventions.AllowAnonymousToPage("/Logout");

    options.Conventions.AuthorizePage("/Diagnostics");
    
    // Allow anonymous access to feedback pages
    options.Conventions.AllowAnonymousToPage("/Feedback/Index");
    options.Conventions.AllowAnonymousToPage("/Feedback/BugReport");
    options.Conventions.AllowAnonymousToPage("/Feedback/Support");
    options.Conventions.AllowAnonymousToPage("/Feedback/General");
    options.Conventions.AllowAnonymousToPage("/Feedback/ThankYou");
    
    options.Conventions.AllowAnonymousToPage("/Shared/Cookies");

    // Allow anonymous access to error pages
    options.Conventions.AllowAnonymousToPage("/Error/NotFound");
    options.Conventions.AllowAnonymousToPage("/Error/Forbidden");
    options.Conventions.AllowAnonymousToPage("/Error/General");
    options.Conventions.AllowAnonymousToPage("/Error/ServerError");
    
    // Allow anonymous access to test pages in non-production environments
    if (builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Test") || builder.Environment.IsStaging())
    {
        options.Conventions.AllowAnonymousToPage("/TestError");
    }
    
    // Allow anonymous access to test login page when test auth is enabled
    if (isTestAuthEnabled)
    {
        options.Conventions.AllowAnonymousToPage("/TestLogin");
        options.Conventions.AllowAnonymousToPage("/TestLogout");
    }
})
.AddSessionStateTempDataProvider();

// Add controllers for API endpoints
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ExternalApiMvcExceptionFilter>();
    
    // Add confirmation interceptor filter globally
});

builder.Services.AddHttpContextAccessor();

// Register Cypress authentication services using CoreLibs pattern
builder.Services.AddKeyedScoped<ICustomRequestChecker, InternalAuthRequestChecker>("internal");

// Add confirmation interceptor filter globally for all MVC actions
builder.Services.Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options =>
{
    options.Filters.Add<GovUK.Dfe.FlexForms.Web.Filters.ConfirmationInterceptorFilter>();
});

// Add hybrid caching (Memory + Redis) with automatic session support
builder.Services.AddHybridCaching(builder.Configuration);
builder.Services.PostConfigure<GovUK.Dfe.CoreLibs.Caching.Settings.CacheSettings>(settings =>
{
    settings.Redis ??= new GovUK.Dfe.CoreLibs.Caching.Settings.RedisCacheSettings();
    settings.Redis.KeyPrefix = GovUK.Dfe.FlexForms.Domain.Caching.FlexFormsCacheKeys.RedisKeyPrefix;
});

// Configure session with timeout settings to prevent hanging/blocking
builder.Services.AddSession(options =>
{
    // Keep ASP.NET session alive longer than the inactivity logout threshold (30 min)
    // so LastActivity timestamps survive until the idle warning / force logout runs.
    options.IdleTimeout = TimeSpan.FromMinutes(45);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IOTimeout = TimeSpan.FromSeconds(5); // Prevent indefinite blocking on session I/O
});

builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.CheckConsentNeeded = context => true;
    options.MinimumSameSitePolicy = SameSiteMode.None;
    options.Secure = CookieSecurePolicy.Always;
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "text/html", "text/css", "application/javascript", "text/javascript" });
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest; // Use faster but less compression
});

builder.Services.Configure<TokenRefreshSettings>(configuration.GetSection("TokenRefresh"));

OpenIdConnectEvents CreateEntraSsoEvents() => new()
{
    OnMessageReceived = context =>
    {
        if (platformBootstrapEnabled)
        {
            TenantAwareEntraSsoConfigurator.ApplyTenantSettings(context.HttpContext, context.Options);
        }

        return Task.CompletedTask;
    },

    OnRedirectToIdentityProvider = async context =>
    {
        if (platformBootstrapEnabled)
        {
            // Must rewrite ProtocolMessage — Options.ClientId alone is too late for the authorize URL.
            await TenantAwareEntraSsoConfigurator.ApplyProtocolMessageAsync(context);
        }
    },

    OnRedirectToIdentityProviderForSignOut = async context =>
    {
        // Handler does not set RequestType; mark logout so tenant overlay keeps end_session.
        context.ProtocolMessage.RequestType = OpenIdConnectRequestType.Logout;

        if (platformBootstrapEnabled)
        {
            await TenantAwareEntraSsoConfigurator.ApplyProtocolMessageAsync(context);
        }

        DfESignInOidcPublicUrls.ApplyPostLogoutRedirectUri(context);
    },

    OnRemoteFailure = async context =>
    {
        var error = context.Failure?.Message ?? "Unknown error";

        if (IsRecoverableOidcRemoteFailure(error, context.Request.Path, "/signout-callback-entra"))
        {
            await CompleteLocalSignOutAsync(context.HttpContext);
            context.Response.Redirect(DfESignInOidcPublicUrls.BuildAbsoluteUrl(context.HttpContext, "/"));
            context.HandleResponse();
            return;
        }
    },

    OnAuthenticationFailed = context =>
    {
        context.HandleResponse();
        context.Response.Redirect("/error?message=" + Uri.EscapeDataString(context.Exception.Message));
        return Task.CompletedTask;
    },

    OnSignedOutCallbackRedirect = async context =>
    {
        await CompleteLocalSignOutAsync(context.HttpContext);
    }
};

// Register both schemes once, and use a dynamic scheme provider to pick per-request
var authenticationBuilder = builder.Services
    .AddAuthentication()
    .AddCookie()
    .AddCustomOpenIdConnect(configuration, sectionName: "DfESignIn", new OpenIdConnectEvents
    {
        OnMessageReceived = context =>
        {
            // Callback (/signin-oidc) must use the same tenant ClientId/audience as the challenge.
            if (platformBootstrapEnabled)
            {
                TenantAwareOpenIdConnectConfigurator.ApplyTenantSettings(context.HttpContext, context.Options);
            }

            return Task.CompletedTask;
        },

        OnRedirectToIdentityProvider = async context =>
        {
            if (platformBootstrapEnabled)
            {
                // Rewrite ProtocolMessage ClientId/RedirectUri — Options alone is too late and
                // leaves a previous tenant's client_id (e.g. RSDExternalApps) on first hop to LSRP.
                await TenantAwareOpenIdConnectConfigurator.ApplyProtocolMessageAsync(context);
            }
        },

        OnRemoteFailure = async context =>
        {
            var error = context.Failure?.Message ?? "Unknown error";

            if (IsRecoverableOidcRemoteFailure(error, context.Request.Path, "/signout-callback-oidc", "/signin-oidc"))
            {
                await CompleteLocalSignOutAsync(context.HttpContext);
                context.Response.Redirect(DfESignInOidcPublicUrls.BuildAbsoluteUrl(context.HttpContext, "/"));
                context.HandleResponse();
                return;
            }
        },

        OnAuthenticationFailed = context =>
        {
            context.HandleResponse();
            context.Response.Redirect("/error?message=" + Uri.EscapeDataString(context.Exception.Message));
            return Task.CompletedTask;
        },

        OnRedirectToIdentityProviderForSignOut = async context =>
        {
            context.ProtocolMessage.RequestType = OpenIdConnectRequestType.Logout;

            if (platformBootstrapEnabled)
            {
                await TenantAwareOpenIdConnectConfigurator.ApplyProtocolMessageAsync(context);
            }

            DfESignInOidcPublicUrls.ApplyPostLogoutRedirectUri(context);
        },

        OnSignedOutCallbackRedirect = async context =>
        {
            await CompleteLocalSignOutAsync(context.HttpContext);
        }
    })
    .AddScheme<TestAuthenticationSchemeOptions, TestAuthenticationHandler>(
        TestAuthenticationHandler.SchemeName,
        options => { })
    .AddScheme<InternalServiceAuthenticationSchemeOptions, InternalServiceAuthenticationHandler>(
        InternalServiceAuthenticationHandler.SchemeName,
        options => { });

// Platform bootstrap keeps host EntraSso.Enabled=false, but RGVisits enables Entra per tenant.
// Always register the Entra scheme under bootstrap so challenges can switch at runtime.
if (platformBootstrapEnabled)
{
    authenticationBuilder.AddPlatformBootstrapEntraSso(configuration, CreateEntraSsoEvents());
}
else
{
    authenticationBuilder.AddEntraSso(
        configuration,
        sectionName: EntraSsoDefaults.ConfigurationSection,
        CreateEntraSsoEvents());
}

// Use DynamicAuthenticationSchemeProvider to route per request
// Checks for Internal Service Auth (forwarder pattern)
// Then Test Auth, then OIDC
builder.Services.AddSingleton<IAuthenticationSchemeProvider, DynamicAuthenticationSchemeProvider>();

// OIDC SignOutScheme must be the app cookie — the handler does not clear the cookie itself
// on the sign-out callback. Also ensure SignedOutCallbackPath is never left null by config bind.
builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>(
    OpenIdConnectDefaults.AuthenticationScheme,
    options =>
    {
        options.SignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        if (!options.SignedOutCallbackPath.HasValue)
        {
            options.SignedOutCallbackPath = "/signout-callback-oidc";
        }

        // Under platform bootstrap the DfESignIn section contains placeholder values.
        // The OIDC handler calls ConfigurationManager.GetConfigurationAsync *before*
        // OnRedirectToIdentityProvider, which fails with IDX20803 against the placeholder.
        // Replace with a StaticConfigurationManager returning a stub; the tenant-aware
        // events (TenantAwareOpenIdConnectConfigurator) replace it per-request.
        if (platformBootstrapEnabled)
        {
            options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(
                new OpenIdConnectConfiguration());
        }
    });

builder.Services.PostConfigure<Microsoft.AspNetCore.Authentication.OpenIdConnect.OpenIdConnectOptions>(
    EntraSsoDefaults.AuthenticationScheme,
    options =>
    {
        options.SignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        if (!options.SignedOutCallbackPath.HasValue)
        {
            options.SignedOutCallbackPath = "/signout-callback-entra";
        }
    });

builder.Services
    .AddApplicationAuthorization(
        configuration,
        policyCustomizations: null,
        apiAuthenticationScheme: null,
        configureResourcePolicies: opts =>
        {
            opts.Actions.AddRange(["Read", "Write"]);
            opts.ClaimType = "permission";
        });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AdminAccessHelper.CanAccessAdminAreaPolicy, policy =>
        policy.RequireAssertion(ctx => AdminAccessHelper.CanAccessAdminArea(ctx.User)));
    options.AddPolicy(AdminAccessHelper.CanManageTemplatesPolicy, policy =>
        policy.RequireAssertion(ctx => AdminAccessHelper.CanManageTemplates(ctx.User)));
    options.AddPolicy(AdminAccessHelper.CanManageUsersPolicy, policy =>
        policy.RequireAssertion(ctx => AdminAccessHelper.CanManageUsers(ctx.User)));
    options.AddPolicy(AdminAccessHelper.CanManageTenantSettingsPolicy, policy =>
        policy.RequireAssertion(ctx => AdminAccessHelper.CanManageTenantSettings(ctx.User)));
});

builder.Services.AddScoped<ICustomClaimProvider, PermissionsClaimProvider>();

builder.Services.AddTokenRefreshWithOidc(configuration, "DfESignIn", "TokenRefresh");

// Add HttpClient for API calls
builder.Services.AddHttpClient();

builder.Services.AddTenantAwarePlatformServices(configuration);

builder.Services.AddScoped<IContributorService, ContributorService>();
builder.Services.AddScoped<IContributorPatternService, ContributorPatternService>();

builder.Services.AddExternalApplicationsApiClients(configuration);

// Register authentication strategies and composite selector (per-request)
builder.Services.AddScoped<OidcAuthenticationStrategy>();
builder.Services.AddScoped<TestAuthenticationStrategy>();
builder.Services.AddScoped<InternalAuthenticationStrategy>();
builder.Services.AddScoped<EntraSsoAuthenticationStrategy>();
builder.Services.AddScoped<IAuthenticationSchemeStrategy, CompositeAuthenticationSchemeStrategy>();

// Register activity-based token refresh services
builder.Services.AddScoped<IUserActivityTracker, UserActivityTracker>();

// Rebrand is always on in GovUk.Frontend.AspNetCore 4.x (Rebrand option removed)
builder.Services.AddGovUkFrontend();
builder.Services.AddSingleton<IActionContextAccessor, ActionContextAccessor>();
builder.Services.AddScoped<IHtmlHelper, HtmlHelper>();
builder.Services.AddWebLayerServices();
builder.Services.AddScoped<IApplicationResponseService, ApplicationResponseService>();

// Persist cookie tickets server-side so AuthenticationProperties (tokens) don't bloat the browser cookie
builder.Services.AddSingleton<ITicketStore, DistributedCacheTicketStore>();
builder.Services.AddSingleton<IPostConfigureOptions<CookieAuthenticationOptions>, ConfigureCookieTicketStore>();

// New refactored services for Clean Architecture
builder.Services.AddScoped<IFieldFormattingService, FieldFormattingService>();
builder.Services.AddScoped<ITemplateManagementService, TemplateManagementService>();
builder.Services.AddScoped<IApplicationStateService, ApplicationStateService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();

// Conditional Logic Services
builder.Services.AddScoped<IConditionalLogicEngine, ConditionalLogicEngine>();
builder.Services.AddScoped<IConditionalLogicOrchestrator, ConditionalLogicOrchestrator>();

// Derived Collection Flow Services
builder.Services.AddScoped<IDerivedCollectionFlowService, DerivedCollectionFlowService>();

builder.Services.AddScoped<IAutocompleteService, AutocompleteService>();
builder.Services.AddScoped<ITemplateSelectionService, TemplateSelectionService>();
builder.Services.AddScoped<IComplexFieldConfigurationService, ComplexFieldConfigurationService>();
builder.Services.AddScoped<IComplexFieldRendererFactory, ComplexFieldRendererFactory>();
builder.Services.AddScoped<IComplexFieldRenderer, AutocompleteComplexFieldRenderer>();
builder.Services.AddScoped<IComplexFieldRenderer, CompositeComplexFieldRenderer>();
builder.Services.AddScoped<IComplexFieldRenderer, UploadComplexFieldRenderer>();

builder.Services.AddSingleton<ITemplateStore, ApiTemplateStore>(); 
builder.Services.AddUserTokenService(configuration);

// Add test token handler and services when test authentication or Cypress is enabled
if (isTestAuthEnabled)
{
    builder.Services.AddScoped<ITestAuthenticationService, TestAuthenticationService>();
}

// Configure Internal Service Auth settings
builder.Services.Configure<InternalServiceAuthOptions>(
    builder.Configuration.GetSection("InternalServiceAuth"));

// Add internal service authentication service (always available)
builder.Services.AddScoped<IInternalServiceAuthenticationService, InternalServiceAuthenticationService>();

builder.Services.AddServiceCaching(configuration);

builder.Services.AddSingleton<IFormTemplateParser, JsonFormTemplateParser>();
builder.Services.AddScoped<IFormTemplateProvider, FormTemplateProvider>();

// Application terminology configuration (customisable per service, e.g. "application" vs "reform plan")
builder.Services.Configure<ApplicationTerminologyOptions>(configuration.GetSection("ApplicationTerminology"));

// Site-wide notification banner (feature flag driven from appsettings)
builder.Services.Configure<NotificationBannerOptions>(configuration.GetSection("NotificationBanner"));

// Dashboard configuration (page size for application list pagination)
builder.Services.Configure<DashboardOptions>(configuration.GetSection("Dashboard"));
// Scoped so tenant-aware IOptions are not captured for the app lifetime.
builder.Services.AddScoped<IApplicationTerminologyProvider, ApplicationTerminologyProvider>();

// Application submission configuration (mapper key and handlers per application)
builder.Services.Configure<ApplicationSubmissionOptions>(configuration.GetSection("ApplicationSubmission"));

builder.Services.AddTenantAwareOptionsAccessors(configuration);

// Event mapping and publishing services
builder.Services.AddSingleton<IEventMappingProvider, EventMappingProvider>();
builder.Services.AddKeyedScoped<IEventDataMapper, EventDataMapper>("Default");
builder.Services.AddScoped<IEventDataMapperFactory, EventDataMapperFactory>();
builder.Services.AddSingleton<IEventTypeRegistry, EventTypeRegistry>();

// Application submission handlers (resolved by key from ApplicationSubmission:Handlers)
builder.Services.AddKeyedScoped<IApplicationSubmittedHandler, PublishEventApplicationSubmittedHandler>("PublishEvent");
builder.Services.AddKeyedScoped<IApplicationSubmittedHandler, NoOpApplicationSubmittedHandler>("NoOp");
builder.Services.AddScoped<IApplicationSubmissionOrchestrator, ApplicationSubmissionOrchestrator>();

builder.Services.AddDfEMassTransit(
    configuration,
    configureConsumers: x =>
    {
        x.AddConsumer<ScanResultConsumer>();
    },
    configureBus: (context, cfg) =>
    {
        // Configure topic names for message types
        cfg.Message<ScanResultEvent>(m => m.SetEntityName(TopicNames.ScanResult));
        cfg.Message<TransferApplicationSubmittedEvent>(m => m.SetEntityName(TopicNames.TransferApplicationSubmitted));

        cfg.UseJsonSerializer();
    },
    configureAzureServiceBus: (context, cfg) =>
    {
        cfg.UseJsonSerializer();

        // Path 3: one shared subscription for the platform Web artefact (all tenants).
        // Prefix is environment-configurable; suffix identifies the topic purpose.
        var subscriptionPrefix = configuration["MassTransit:SubscriptionPrefix"] ?? "extweb";
        cfg.SubscriptionEndpoint<ScanResultEvent>($"{subscriptionPrefix}-scan-result", e =>
        {
            e.UseMessageRetry(r =>
            {
                // For MessageNotForThisInstanceException (instance filtering in Local env)
                // Retry immediately and frequently so other consumers pick it up fast
                r.Handle<MessageNotForThisInstanceException>();
                r.Immediate(10); // Try 10 times (supports up to 10 concurrent local developers)

                // For all OTHER exceptions (real errors)
                // Retry with delay for transient issues
                r.Ignore<MessageNotForThisInstanceException>(); // Don't apply interval retry to this
                r.Interval(3, TimeSpan.FromSeconds(5)); // 3 retries, 5 seconds apart for real errors
            });

            e.ConfigureConsumeTopology = false;
            e.ConfigureConsumer<ScanResultConsumer>(context);
        });
    });

// Add global exception handler to log crashes before app dies
AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
{
    var exception = args.ExceptionObject as Exception;
    var loggerFactory = builder.Services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("UnhandledException");
    logger.LogCritical(exception, 
        "UNHANDLED EXCEPTION - App is crashing! IsTerminating: {IsTerminating}, Exception Type: {ExceptionType}, Memory: {MemoryMB} MB",
        args.IsTerminating, 
        exception?.GetType().FullName ?? "Unknown",
        GC.GetTotalMemory(false) / 1024 / 1024);
};

var app = builder.Build();

app.UseForwardedHeaders();
app.UsePlatformTenantConfiguration();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error/ServerError");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
else
{
    // In development, still show custom error page but with more details in logs
    app.UseExceptionHandler("/Error/ServerError");
}

// Health probes (App Gateway / ACA) often use HTTP; do not redirect them to HTTPS.
app.UseWhen(
    static context =>
    {
        var path = context.Request.Path;
        return !(path.Equals("/health", StringComparison.OrdinalIgnoreCase)
                 || path.Equals("/healthz", StringComparison.OrdinalIgnoreCase)
                 || path.Equals("/liveness", StringComparison.OrdinalIgnoreCase)
                 || path.Equals("/readiness", StringComparison.OrdinalIgnoreCase));
    },
    static branch => branch.UseHttpsRedirection());
app.UseResponseCompression();

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        const int days = 30;
        ctx.Context.Response.Headers["Cache-Control"] = $"public, max-age={days * 24 * 60 * 60}";
    }
});

app.UseRouting();
app.UseCookiePolicy();

app.UseSession();

app.UseStatusCodePages(ctx =>
{
    if (AuthenticationPathExclusions.ShouldSkip(ctx.HttpContext.Request.Path))
    {
        return Task.CompletedTask;
    }

    var c = ctx.HttpContext.Response.StatusCode;
    if (c == 401) ctx.HttpContext.Response.Redirect("/Error/Forbidden");
    else if (c == 403) ctx.HttpContext.Response.Redirect("/Error/Forbidden");
    else if (c == 404) ctx.HttpContext.Response.Redirect("/Error/NotFound");
    else if (c == 500) ctx.HttpContext.Response.Redirect("/Error/ServerError");
    else if (c >= 500 && c < 600) ctx.HttpContext.Response.Redirect("/Error/ServerError"); // All 5xx errors
    return Task.CompletedTask;
});

app.UseAuthentication();
app.UseTokenManagementMiddleware();
app.UseActivityBasedTokenRefresh(); // Session management: idle timeout 30min, absolute timeout 8hr, token refresh at 30min remaining
app.UsePermissionsCache();
app.UseAuthorization();
app.UseTemplateSelection();

app.MapRazorPages();
app.MapControllers();

// Liveness probe: no tenant resolution, no auth. Used by App Gateway / Container Apps probes.
app.MapGet("/health", () => Results.Text("Healthy", "text/plain"))
    .AllowAnonymous();
app.MapGet("/healthz", () => Results.Text("Healthy", "text/plain"))
    .AllowAnonymous();
app.MapGet("/liveness", () => Results.Text("Healthy", "text/plain"))
    .AllowAnonymous();

// Landing goes through template selection gate (middleware) then dashboard.
app.MapGet("/", context =>
{
    context.Response.Redirect("/applications/dashboard");
    return Task.CompletedTask;
});

app.UseGovUkFrontend();

// TokenManagementMiddleware now handles all logout logic internally
// No additional token expiry handlers needed

await app.RunAsync();


[ExcludeFromCodeCoverage]
public static partial class Program
{
    /// <summary>
    /// Clears the local auth cookie and session after IdP sign-out (or recoverable failure).
    /// </summary>
    internal static async Task CompleteLocalSignOutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        httpContext.Session.Clear();
    }

    /// <summary>
    /// Treats common post-logout OIDC failures (missing correlation/state) as successful sign-out.
    /// </summary>
    internal static bool IsRecoverableOidcRemoteFailure(
        string error,
        PathString requestPath,
        params string[] recoverablePaths)
    {
        if (error.Contains("message.State", StringComparison.OrdinalIgnoreCase)
            || error.Contains("Correlation failed", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return recoverablePaths.Any(path => requestPath.StartsWithSegments(path));
    }
}