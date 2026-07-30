using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Infrastructure.Parsers;
using GovUK.Dfe.FlexForms.Infrastructure.Providers;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using GovUK.Dfe.FlexForms.Infrastructure.Stores;
using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Interfaces;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Api.Client;
using GovUK.Dfe.FlexForms.Api.Client.Contracts;
using GovUK.Dfe.FlexForms.Api.Client.Extensions;

namespace GovUK.Dfe.FlexForms.Web.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddExternalApplicationsApiClients(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var platformEnabled = configuration
                .GetSection(PlatformBootstrapOptions.SectionName)
                .Get<PlatformBootstrapOptions>()?.Enabled ?? false;

            bool? enableTokenExchange = platformEnabled ? true : null;

            services.AddExternalApplicationsApiClient<ITokensClient, TokensClient>(
                configuration, enableTokenExchange: enableTokenExchange);
            services.AddExternalApplicationsApiClient<IUsersClient, UsersClient>(
                configuration, enableTokenExchange: enableTokenExchange);
            services.AddExternalApplicationsApiClient<IRolesClient, RolesClient>(
                configuration, enableTokenExchange: enableTokenExchange);
            services.AddExternalApplicationsApiClient<IApplicationsClient, ApplicationsClient>(
                configuration, enableTokenExchange: enableTokenExchange);
            services.AddExternalApplicationsApiClient<ITemplatesClient, TemplatesClient>(
                configuration, enableTokenExchange: enableTokenExchange);
            services.AddExternalApplicationsApiClient<IHubAuthClient, HubAuthClient>(
                configuration, enableTokenExchange: enableTokenExchange);
            services.AddExternalApplicationsApiClient<INotificationsClient, NotificationsClient>(
                configuration, enableTokenExchange: enableTokenExchange);
            services.AddExternalApplicationsApiClient<IUserFeedbackClient, UserFeedbackClient>(
                configuration, enableTokenExchange: enableTokenExchange);
            services.AddExternalApplicationsApiClient<ITenantAdminClient, TenantAdminClient>(
                configuration, enableTokenExchange: enableTokenExchange);
            return services;
        }

        public static IServiceCollection AddWebLayerServices(this IServiceCollection services)
        {
            // Web layer services
            services.AddScoped<IFieldRendererService, FieldRendererService>();
            services.AddScoped<IFormErrorStore, FormErrorStore>();

            // Infrastructure/application services used by web
            services.AddScoped<IApplicationResponseService, ApplicationResponseService>();
            services.AddScoped<IFieldFormattingService, FieldFormattingService>();
            services.AddScoped<ITemplateManagementService, TemplateManagementService>();
            services.AddScoped<IContributorPatternService, ContributorPatternService>();
            services.AddScoped<IApplicationStateService, ApplicationStateService>();
            services.AddScoped<IFileUploadService, FileUploadService>();
            services.AddScoped<IAutocompleteService, AutocompleteService>();
            services.AddScoped<IApplicationStatusService, ApplicationStatusService>();
            services.AddScoped<IComplexFieldConfigurationService, ComplexFieldConfigurationService>();
            services.AddScoped<IComplexFieldRendererFactory, ComplexFieldRendererFactory>();
            services.AddScoped<IComplexFieldRenderer, AutocompleteComplexFieldRenderer>();
            services.AddScoped<IComplexFieldRenderer, CompositeComplexFieldRenderer>();
            services.AddScoped<IComplexFieldRenderer, UploadComplexFieldRenderer>();
            services.AddSingleton<ITemplateStore, ApiTemplateStore>();
            services.AddSingleton<IFormTemplateParser, JsonFormTemplateParser>();
            services.AddScoped<IFormTemplateProvider, FormTemplateProvider>();

            // Form Engine Services
            services.AddScoped<IFormStateManager, FormStateManager>();
            services.AddScoped<IFormNavigationService, FormNavigationService>();
            services.AddScoped<INavigationHistoryService, NavigationHistoryService>();
            services.AddScoped<IFormDataManager, FormDataManager>();
            services.AddScoped<IFieldRequirementService, FieldRequirementService>();
            services.AddScoped<IFormValidationOrchestrator, GovUK.Dfe.FlexForms.Infrastructure.Services.FormValidationOrchestrator>();
            services.AddScoped<IFormConfigurationService, FormConfigurationService>();
            services.AddScoped<ITemplateValidationService, TemplateValidationService>();
            services.AddHttpContextAccessor();

            // Confirmation Services
            services.AddScoped<IButtonConfirmationService, ButtonConfirmationService>();
            services.AddScoped<IConfirmationDataService, ConfirmationDataService>();

            // Feedback services
            services.AddScoped<IFeedbackService, FeedbackService>();

            return services;
        }
    }
}

