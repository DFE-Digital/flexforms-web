using GovUK.Dfe.FlexForms.Domain.Models;

namespace GovUK.Dfe.FlexForms.Web.Services;

public interface IAutocompleteAccessTokenProvider
{
    Task<string?> GetAccessTokenAsync(
        ComplexFieldConfiguration configuration,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default);
}
