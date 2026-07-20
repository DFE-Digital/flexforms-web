using GovUK.Dfe.FlexForms.Web.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Web.Services
{
    public class FormErrorStore(IHttpContextAccessor httpContextAccessor) : IFormErrorStore
    {
        private const string Prefix = "FormErrors_";
        private const string GeneralSuffix = "_General";

        public void Save(string contextKey, ModelStateDictionary modelState, string? generalError = null)
        {
            var http = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No HttpContext available");

            var dict = new Dictionary<string, List<string>>();
            foreach (var kv in modelState)
            {
                if (kv.Value.Errors.Count > 0)
                {
                    dict[kv.Key] = kv.Value.Errors.Select(e => e.ErrorMessage).ToList();
                }
            }

            var payload = JsonSerializer.Serialize(dict);
            http.Session.SetString(Prefix + contextKey, payload);

            if (!string.IsNullOrWhiteSpace(generalError))
            {
                http.Session.SetString(Prefix + contextKey + GeneralSuffix, generalError);
            }
        }

        public (Dictionary<string, List<string>> FieldErrors, string? GeneralError) Load(string contextKey, bool clearAfterRead = true)
        {
            var http = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No HttpContext available");
            var errorsJson = http.Session.GetString(Prefix + contextKey);
            var general = http.Session.GetString(Prefix + contextKey + GeneralSuffix);

            var fieldErrors = string.IsNullOrEmpty(errorsJson)
                ? new Dictionary<string, List<string>>()
                : (JsonSerializer.Deserialize<Dictionary<string, List<string>>>(errorsJson) ?? new Dictionary<string, List<string>>());

            if (clearAfterRead)
            {
                http.Session.Remove(Prefix + contextKey);
                http.Session.Remove(Prefix + contextKey + GeneralSuffix);
            }

            return (fieldErrors, general);
        }

        public void Clear(string contextKey)
        {
            var http = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No HttpContext available");
            http.Session.Remove(Prefix + contextKey);
            http.Session.Remove(Prefix + contextKey + GeneralSuffix);
        }
    }
}


