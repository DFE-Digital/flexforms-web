using GovUK.Dfe.CoreLibs.Security.Configurations;

namespace GovUK.Dfe.FlexForms.Web.Services;

/// <summary>
/// Resolves <see cref="InternalServiceAuthOptions"/> for the current HTTP request.
/// Prefer tenant TenantConfig values; fall back to host bootstrap settings when the
/// tenant section is missing (local / transitional setups).
/// </summary>
public interface IInternalServiceAuthOptionsResolver
{
    /// <summary>
    /// Gets the effective InternalServiceAuth options for the current request tenant.
    /// </summary>
    /// <returns>Bound options; never null.</returns>
    InternalServiceAuthOptions Resolve();
}
