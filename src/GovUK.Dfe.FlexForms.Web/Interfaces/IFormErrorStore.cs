using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace GovUK.Dfe.FlexForms.Web.Interfaces
{
    /// <summary>
    /// Persists and restores ModelState and general errors between separate Razor Page posts
    /// where the UI is rendered in a different page/partial from the PageModel handler.
    /// </summary>
    public interface IFormErrorStore
    {
        /// <summary>
        /// Saves ModelState errors and an optional general error for a given context key.
        /// </summary>
        void Save(string contextKey, ModelStateDictionary modelState, string? generalError = null);

        /// <summary>
        /// Loads previously saved errors for the given context key.
        /// </summary>
        (Dictionary<string, List<string>> FieldErrors, string? GeneralError) Load(string contextKey, bool clearAfterRead = true);

        /// <summary>
        /// Clears any saved errors for the given context key.
        /// </summary>
        void Clear(string contextKey);
    }
}


