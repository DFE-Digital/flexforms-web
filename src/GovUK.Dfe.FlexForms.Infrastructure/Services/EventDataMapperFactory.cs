using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// Resolves IEventDataMapper by key from ApplicationSubmission:MapperKey using keyed services.
/// </summary>
public class EventDataMapperFactory(
    IServiceProvider serviceProvider,
    IOptions<ApplicationSubmissionOptions> options,
    ILogger<EventDataMapperFactory> logger) : IEventDataMapperFactory
{
    /// <inheritdoc />
    public IEventDataMapper GetMapper(CancellationToken cancellationToken = default)
    {
        var key = options.Value.MapperKey ?? "Default";
        var mapper = serviceProvider.GetKeyedService<IEventDataMapper>(key);
        if (mapper == null)
        {
            logger.LogWarning("No IEventDataMapper registered for key '{MapperKey}'. Using key 'Default'.", key);
            mapper = serviceProvider.GetKeyedService<IEventDataMapper>("Default");
        }

        if (mapper == null)
        {
            throw new InvalidOperationException(
                $"No IEventDataMapper registered for key '{key}'. Register keyed IEventDataMapper in DI (e.g. AddKeyedScoped<IEventDataMapper, EventDataMapper>(\"Default\")).");
        }

        return mapper;
    }
}
