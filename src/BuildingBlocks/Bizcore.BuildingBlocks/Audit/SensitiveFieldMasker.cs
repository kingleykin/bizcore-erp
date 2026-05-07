using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bizcore.BuildingBlocks.Audit
{
    /// <summary>
    /// Redacts sensitive fields from objects before they are serialized into audit JSON snapshots.
    /// Prevents credentials, tokens, and PII from being persisted in the audit trail.
    /// </summary>
    public static class SensitiveFieldMasker
    {
        private static readonly HashSet<string> _sensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "password", "passwordhash", "hashedpassword",
            "token", "accesstoken", "refreshtoken", "idtoken",
            "secret", "clientsecret", "appsecret",
            "cardnumber", "cvv", "cvc", "expirydate",
            "ssn", "socialsecuritynumber",
            "pin", "otp",
            "connectionstring", "apikey"
        };

        private const string MaskValue = "***";

        /// <summary>
        /// Serializes an object to JSON, masking any known sensitive fields.
        /// </summary>
        public static string? ToMaskedJson(object? obj)
        {
            if (obj is null) return null;

            try
            {
                var json = JsonSerializer.Serialize(obj, new JsonSerializerOptions
                {
                    WriteIndented = false,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                return MaskJsonString(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Masks sensitive fields in an existing JSON string.
        /// Handles both string values and nested structures via regex on string values.
        /// </summary>
        public static string MaskJsonString(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return json;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var result = MaskElement(doc.RootElement);
                return JsonSerializer.Serialize(result);
            }
            catch
            {
                // Fallback: regex-based masking for malformed JSON
                return RegexMask(json);
            }
        }

        private static object? MaskElement(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Object => MaskObject(element),
                JsonValueKind.Array  => MaskArray(element),
                _                    => GetPrimitiveValue(element)
            };
        }

        private static Dictionary<string, object?> MaskObject(JsonElement element)
        {
            var dict = new Dictionary<string, object?>();
            foreach (var prop in element.EnumerateObject())
            {
                dict[prop.Name] = _sensitiveKeys.Contains(prop.Name)
                    ? MaskValue
                    : MaskElement(prop.Value);
            }
            return dict;
        }

        private static List<object?> MaskArray(JsonElement element)
        {
            return element.EnumerateArray().Select(MaskElement).ToList();
        }

        private static object? GetPrimitiveValue(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.String  => element.GetString(),
                JsonValueKind.Number  => element.TryGetInt64(out var l) ? l : element.GetDouble(),
                JsonValueKind.True    => true,
                JsonValueKind.False   => false,
                JsonValueKind.Null    => null,
                _                    => element.GetRawText()
            };
        }

        private static string RegexMask(string json)
        {
            var pattern = $@"""(?:{string.Join("|", _sensitiveKeys.Select(Regex.Escape))})"":\s*""[^""]*""";
            return Regex.Replace(json, pattern,
                m => Regex.Replace(m.Value, @"""[^""]*""$", $"\"{MaskValue}\""),
                RegexOptions.IgnoreCase);
        }
    }
}
