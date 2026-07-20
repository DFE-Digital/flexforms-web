using GovUK.Dfe.FlexForms.Application.Interfaces;
using GovUK.Dfe.FlexForms.Domain.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text.Json;

namespace GovUK.Dfe.FlexForms.Infrastructure.Services
{
    /// <summary>
    /// Implementation of the button confirmation service
    /// </summary>
    public class ButtonConfirmationService : IButtonConfirmationService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IConfirmationDataService _dataService;
        private readonly ILogger<ButtonConfirmationService> _logger;

        public ButtonConfirmationService(
            IHttpContextAccessor httpContextAccessor,
            IConfirmationDataService dataService,
            ILogger<ButtonConfirmationService> logger)
        {
            _httpContextAccessor = httpContextAccessor;
            _dataService = dataService;
            _logger = logger;
        }

        /// <summary>
        /// Creates a confirmation request and returns a token
        /// </summary>
        /// <param name="request">The confirmation request details</param>
        /// <returns>A unique confirmation token</returns>
        public string CreateConfirmation(ConfirmationRequest request)
        {
            var token = GenerateSecureToken();
            request.ConfirmationToken = token;

            var context = new ConfirmationContext
            {
                Token = token,
                Request = request,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10) // 10 minute expiry
            };

            // Store in session
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session != null)
            {
                var key = $"Confirmation_{token}";
                var serializedContext = JsonSerializer.Serialize(context);
                session.SetString(key, serializedContext);

                _logger.LogInformation("Created confirmation with token {Token} for handler {Handler}",
                    token, request.OriginalHandler);
            }
            else
            {
                _logger.LogError("Unable to create confirmation - HttpContext or Session is null");
                throw new InvalidOperationException("Session is not available");
            }

            return token;
        }

        /// <summary>
        /// Retrieves a confirmation context by token
        /// </summary>
        /// <param name="token">The confirmation token</param>
        /// <returns>The confirmation context or null if not found/expired</returns>
        public ConfirmationContext? GetConfirmation(string token)
        {
            if (string.IsNullOrEmpty(token))
                return null;

            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null)
                return null;

            var key = $"Confirmation_{token}";
            var data = session.GetString(key);

            if (string.IsNullOrEmpty(data))
            {
                _logger.LogWarning("Confirmation token {Token} not found in session", token);
                return null;
            }

            try
            {
                var context = JsonSerializer.Deserialize<ConfirmationContext>(data);
                if (context?.IsExpired == true)
                {
                    _logger.LogWarning("Confirmation token {Token} has expired", token);
                    session.Remove(key);
                    return null;
                }

                return context;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize confirmation context for token {Token}", token);
                session.Remove(key);
                return null;
            }
        }

        /// <summary>
        /// Prepares the display model for the confirmation page
        /// </summary>
        /// <param name="token">The confirmation token</param>
        /// <returns>The display model or null if token is invalid</returns>
        public ConfirmationDisplayModel? PrepareDisplayModel(string token)
        {
            var context = GetConfirmation(token);
            if (context == null)
                return null;

            // Normalize form data to primitive types so the confirmation view can re-post it
            var normalizedFormData = NormalizeFormData(context.Request.OriginalFormData);

            var displayData = _dataService.FormatDisplayData(
                normalizedFormData,
                context.Request.DisplayFields);

            return new ConfirmationDisplayModel
            {
                Title = string.IsNullOrWhiteSpace(context.Request.Title) ? "Confirm your action" : context.Request.Title!,
                RequiredMessage = string.IsNullOrWhiteSpace(context.Request.RequiredMessage) ? "Select yes if you want to continue": context.Request.RequiredMessage!,
                DisplayData = displayData,
                ReturnUrl = context.Request.ReturnUrl,
                ConfirmationToken = token,
                OriginalActionUrl = $"{context.Request.OriginalPagePath}?handler={context.Request.OriginalHandler}",
                OriginalFormData = normalizedFormData
            };
        }

        /// <summary>
        /// Converts deserialized form data (which may contain JsonElement) into strings or string arrays
        /// suitable for reposting from the confirmation page.
        /// </summary>
        /// <param name="formData">Original form data from the intercepted request</param>
        /// <returns>Dictionary with values as string or string[]</returns>
        private static Dictionary<string, object> NormalizeFormData(Dictionary<string, object> formData)
        {
            var result = new Dictionary<string, object>();

            if (formData == null || formData.Count == 0)
            {
                return result;
            }

            // Exclude antiforgery field; the confirmation page emits its own token
            var keysToSkip = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "__RequestVerificationToken"
            };

            foreach (var kvp in formData)
            {
                var key = kvp.Key;
                var value = kvp.Value;

                if (keysToSkip.Contains(key))
                {
                    continue;
                }

                switch (value)
                {
                    case string s:
                        result[key] = s;
                        TryAugmentFromJsonString(s, result);
                        break;
                    case string[] arr:
                        result[key] = arr;
                        break;
                    case JsonElement je:
                        var converted = ConvertJsonElement(je);
                        result[key] = converted;
                        if (converted is string cs)
                        {
                            TryAugmentFromJsonString(cs, result);
                        }
                        break;
                    default:
                        result[key] = value?.ToString() ?? string.Empty;
                        break;
                }
            }

            return result;
        }

        private static object ConvertJsonElement(JsonElement je)
        {
            switch (je.ValueKind)
            {
                case JsonValueKind.String:
                    return je.GetString() ?? string.Empty;
                case JsonValueKind.Number:
                    return je.ToString();
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return je.GetBoolean().ToString();
                case JsonValueKind.Array:
                {
                    var list = new List<string>();
                    foreach (var item in je.EnumerateArray())
                    {
                        list.Add(item.ToString());
                    }
                    return list.ToArray();
                }
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return string.Empty;
                default:
                    return je.ToString();
            }
        }

        /// <summary>
        /// If the provided string contains a JSON object with well-known fields (name, postcode, ukprn, companiesHouseNumber),
        /// augment the flattened form data with those keys for easier confirmation display.
        /// Adds common key variants (e.g., trustname/trustName, companiesHousenumber/companiesHouseNumber, postCode/postcode).
        /// </summary>
        private static void TryAugmentFromJsonString(string value, Dictionary<string, object> sink)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            value = value.Trim();
            if (!(value.StartsWith("{") && value.EndsWith("}"))) return;

            try
            {
                using var doc = JsonDocument.Parse(value);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
                var root = doc.RootElement;

                string? name = null;
                string? ukprn = null;
                string? code = null;
                string? chNo = null;
                string? postcode = null;

                if (root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                    name = n.GetString();
                if (root.TryGetProperty("ukprn", out var u) && (u.ValueKind == JsonValueKind.String || u.ValueKind == JsonValueKind.Number))
                    ukprn = u.ToString();
                if (root.TryGetProperty("code", out var laCode) && (laCode.ValueKind == JsonValueKind.String || laCode.ValueKind == JsonValueKind.Number))
                    code = laCode.ToString();
                if (root.TryGetProperty("companiesHouseNumber", out var c) && c.ValueKind == JsonValueKind.String)
                    chNo = c.GetString();
                if (root.TryGetProperty("postcode", out var pc) && pc.ValueKind == JsonValueKind.String)
                    postcode = pc.GetString();
                if (string.IsNullOrWhiteSpace(postcode) && root.TryGetProperty("postCode", out var pc2) && pc2.ValueKind == JsonValueKind.String)
                    postcode = pc2.GetString();
                // Nested address object (e.g. DfE establishments API: address.postcode)
                if (string.IsNullOrWhiteSpace(postcode) && root.TryGetProperty("address", out var addr) && addr.ValueKind == JsonValueKind.Object)
                {
                    if (addr.TryGetProperty("postcode", out var apc) && apc.ValueKind == JsonValueKind.String)
                        postcode = apc.GetString();
                    if (string.IsNullOrWhiteSpace(postcode) && addr.TryGetProperty("postCode", out var apc2) && apc2.ValueKind == JsonValueKind.String)
                        postcode = apc2.GetString();
                    if (string.IsNullOrWhiteSpace(postcode) && addr.TryGetProperty("postalCode", out var apc3) && apc3.ValueKind == JsonValueKind.String)
                        postcode = apc3.GetString();
                }

                // Only add if not already present
                void AddIfMissing(string key, string? val)
                {
                    if (string.IsNullOrWhiteSpace(val)) return;
                    if (!sink.ContainsKey(key)) sink[key] = val;
                }

                AddIfMissing("trustName", name);
                AddIfMissing("trustname", name);
                AddIfMissing("name", name);
                AddIfMissing("postcode", postcode);
                AddIfMissing("ukprn", ukprn);
                AddIfMissing("code", code);
                AddIfMissing("companiesHouseNumber", chNo);
                AddIfMissing("companiesHousenumber", chNo); // tolerate common misspelling
            }
            catch
            {
                // ignore parsing errors; nothing to augment
            }
        }

        /// <summary>
        /// Clears an expired or used confirmation from storage
        /// </summary>
        /// <param name="token">The confirmation token to clear</param>
        public void ClearConfirmation(string token)
        {
            if (string.IsNullOrEmpty(token))
                return;

            var session = _httpContextAccessor.HttpContext?.Session;
            if (session != null)
            {
                var key = $"Confirmation_{token}";
                session.Remove(key);
                _logger.LogInformation("Cleared confirmation token {Token}", token);
            }
        }

        /// <summary>
        /// Validates that a confirmation token is valid and not expired
        /// </summary>
        /// <param name="token">The confirmation token</param>
        /// <returns>True if the token is valid and not expired</returns>
        public bool IsValidToken(string token)
        {
            var context = GetConfirmation(token);
            return context != null && !context.IsExpired;
        }

        /// <summary>
        /// Generates a cryptographically secure token
        /// </summary>
        /// <returns>A secure token string</returns>
        private static string GenerateSecureToken()
        {
            // Use hex encoding to avoid WAF triggers (e.g., "--" SQL comment sequences)
            var bytes = RandomNumberGenerator.GetBytes(32);
            var c = new char[bytes.Length * 2];
            int i = 0;
            foreach (var b in bytes)
            {
                c[i++] = GetHexNibble(b >> 4);
                c[i++] = GetHexNibble(b & 0xF);
            }
            return new string(c);
        }

        private static char GetHexNibble(int value)
        {
            return (char)(value < 10 ? ('0' + value) : ('a' + (value - 10)));
        }
    }
}
