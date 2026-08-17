using GovUK.Dfe.FlexForms.Application.Admin;
using GovUK.Dfe.FlexForms.Application.Dashboard;
using GovUK.Dfe.FlexForms.Application.FormEngine;
using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Infrastructure;
using GovUK.Dfe.FlexForms.Web.Configuration;
using GovUK.Dfe.FlexForms.Web.Interfaces;
using GovUK.Dfe.FlexForms.Web.Services;
using GovUK.Dfe.FlexForms.Web.ViewModels.FormEngine;
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
            services.AddInfrastructureDependencyGroup();

            services.AddScoped<IFieldRendererService, FieldRendererService>();
            services.AddScoped<IFormErrorStore, FormErrorStore>();
            services.AddScoped<IAutocompleteService, AutocompleteService>();
            services.AddScoped<IApplicationStatusService, ApplicationStatusService>();
            services.AddScoped<ITemplateSelectionService, TemplateSelectionService>();

            services.AddScoped<ICollectionFlowProgressStore, CollectionFlowProgressStore>();
            services.AddScoped<IInfectedUploadFilter, InfectedUploadFilter>();
            services.AddScoped<IFormFileFieldService, FormFileFieldService>();
            services.AddSingleton<IPostedFormDataBinder, PostedFormDataBinder>();
            services.AddScoped<ICompleteFormTask, CompleteFormTaskService>();
            services.AddScoped<ISubmitFormApplication, SubmitFormApplicationService>();
            services.AddScoped<IPrepareFormEngineGet, PrepareFormEngineGetService>();
            services.AddScoped<ISaveFormPage, SaveFormPageService>();
            services.AddScoped<IRemoveCollectionItem, RemoveCollectionItemService>();
            services.AddScoped<IUploadFormFile, UploadFormFileService>();
            services.AddScoped<IDeleteFormFile, DeleteFormFileService>();
            services.AddScoped<IDownloadFormFile, DownloadFormFileService>();
            services.AddScoped<ITenantSettingsAdmin, TenantSettingsAdminService>();
            services.AddScoped<IEventMappingsAdmin, EventMappingsAdminService>();
            services.AddScoped<ITemplateManagerAdmin, TemplateManagerAdminService>();
            services.AddScoped<IDuplicateTenantAdmin, DuplicateTenantAdminService>();
            services.AddScoped<IAdminHome, AdminHomeService>();
            services.AddScoped<IOrganisationSettingsAdmin, OrganisationSettingsAdminService>();
            services.AddScoped<ICustomStatusLabelOverridesAdmin, CustomStatusLabelOverridesAdminService>();
            services.AddScoped<IContributorManagementAdmin, ContributorManagementAdminService>();
            services.AddScoped<IDashboardApplications, DashboardApplicationsService>();
            services.AddScoped<IUserManagerAdmin, UserManagerAdminService>();
            services.AddScoped<IUserManagerAddAdmin, UserManagerAddAdminService>();
            services.AddScoped<IUserManagerEditAdmin, UserManagerEditAdminService>();
            services.AddScoped<IUserManagerPermissionsAdmin, UserManagerPermissionsAdminService>();
            services.AddScoped<IRoleManagerAdmin, RoleManagerAdminService>();
            services.AddScoped<IRoleManagerPermissionsAdmin, RoleManagerPermissionsAdminService>();
            services.AddScoped<IFormEnginePresentationComposer, FormEnginePresentationComposer>();

            return services;
        }
    }
}
