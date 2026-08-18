namespace GovUK.Dfe.FlexForms.Web.Tenancy;

/// <summary>
/// Carries the current tenant into singleton HttpClient handlers when there is no HTTP request
/// (MassTransit consumers). HTTP requests continue to use <see cref="IHttpContextAccessor"/>.
/// </summary>
public static class AmbientTenantRequestContext
{
    private static readonly AsyncLocal<ITenantRequestContext?> Current = new();

    public static ITenantRequestContext? Value => Current.Value;

    public static IDisposable Use(ITenantRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var previous = Current.Value;
        Current.Value = context;
        return new Restorer(previous);
    }

    private sealed class Restorer(ITenantRequestContext? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
