using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Application.Models;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services;

/// <summary>
/// No-op submission handler. Use when Handlers list is empty or for tests.
/// </summary>
public class NoOpApplicationSubmittedHandler : IApplicationSubmittedHandler
{
    /// <inheritdoc />
    public Task HandleAsync(ApplicationSubmittedContext context, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
