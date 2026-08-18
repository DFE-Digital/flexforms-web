using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Infrastructure.Parsers;
using GovUK.Dfe.FlexForms.Infrastructure.Providers;
using GovUK.Dfe.FlexForms.Infrastructure.Services;
using GovUK.Dfe.FlexForms.Infrastructure.Stores;
using Microsoft.Extensions.DependencyInjection;

namespace GovUK.Dfe.FlexForms.Infrastructure;

/// <summary>
/// Registers Infrastructure adapters. Call once from the composition root.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureDependencyGroup(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<IApplicationResponseService, ApplicationResponseService>();
        services.AddScoped<IFieldFormattingService, FieldFormattingService>();
        services.AddScoped<ITemplateManagementService, TemplateManagementService>();
        services.AddScoped<IContributorService, ContributorService>();
        services.AddScoped<IContributorPatternService, ContributorPatternService>();
        services.AddScoped<IApplicationStateService, ApplicationStateService>();
        services.AddScoped<IFileUploadService, FileUploadService>();
        services.AddScoped<IComplexFieldConfigurationService, ComplexFieldConfigurationService>();
        services.AddScoped<IComplexFieldRendererFactory, ComplexFieldRendererFactory>();
        services.AddScoped<IComplexFieldRenderer, AutocompleteComplexFieldRenderer>();
        services.AddScoped<IComplexFieldRenderer, CompositeComplexFieldRenderer>();
        services.AddScoped<IComplexFieldRenderer, UploadComplexFieldRenderer>();
        services.AddSingleton<ITemplateStore, ApiTemplateStore>();
        services.AddSingleton<IFormTemplateParser, JsonFormTemplateParser>();
        services.AddScoped<IFormTemplateProvider, FormTemplateProvider>();
        services.AddScoped<IFormStateManager, FormStateManager>();
        services.AddScoped<IFormNavigationService, FormNavigationService>();
        services.AddScoped<INavigationHistoryService, NavigationHistoryService>();
        services.AddScoped<IFormSessionStore, HttpFormSessionStore>();
        services.AddSingleton<IInfectedFileStore, RedisInfectedFileStore>();
        services.AddScoped<IFormValidationOrchestrator, FormValidationOrchestrator>();
        services.AddScoped<ITemplateValidationService, TemplateValidationService>();
        services.AddScoped<IFieldRequirementService, FieldRequirementService>();
        services.AddScoped<IButtonConfirmationService, ButtonConfirmationService>();
        services.AddScoped<IConfirmationDataService, ConfirmationDataService>();
        services.AddScoped<IFeedbackService, FeedbackService>();
        services.AddScoped<IConditionalLogicEngine, ConditionalLogicEngine>();
        services.AddScoped<IConditionalLogicOrchestrator, ConditionalLogicOrchestrator>();
        services.AddScoped<IDerivedCollectionFlowService, DerivedCollectionFlowService>();
        services.AddScoped<IApplicationTerminologyProvider, ApplicationTerminologyProvider>();
        services.AddScoped<ISchemaEventDefinitionProvider, SchemaEventDefinitionProvider>();
        services.AddSingleton<IEventTypeRegistry, EventTypeRegistry>();

        return services;
    }
}
