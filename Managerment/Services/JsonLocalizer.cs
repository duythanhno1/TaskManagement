using System.Text.Json;
using Managerment.Interfaces;

namespace Managerment.Services
{
    public class JsonLocalizer : ILocalizer
    {
        private readonly Dictionary<string, Dictionary<string, string>> _resources = new();
        private readonly IHttpContextAccessor _httpContextAccessor;
        private const string DefaultLang = "en";
        private static readonly string[] SupportedLangs = { "vi", "en", "ja" };

        public JsonLocalizer(IHttpContextAccessor httpContextAccessor, IWebHostEnvironment env)
        {
            _httpContextAccessor = httpContextAccessor;

            var resourcePath = Path.Combine(env.ContentRootPath, "Resources");

            foreach (var lang in SupportedLangs)
            {
                var filePath = Path.Combine(resourcePath, $"{lang}.json");
                if (File.Exists(filePath))
                {
                    var json = File.ReadAllText(filePath);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null)
                    {
                        _resources[lang] = dict;
                    }
                }
            }
        }

        public string Get(string key, params object[] args)
        {
            var lang = GetCurrentLanguage();

            // Try requested language → fallback to English → fallback to key
            if (_resources.TryGetValue(lang, out var langDict) && langDict.TryGetValue(key, out var message))
            {
                return args.Length > 0 ? string.Format(message, args) : message;
            }

            if (lang != DefaultLang && _resources.TryGetValue(DefaultLang, out var defaultDict) && defaultDict.TryGetValue(key, out var fallback))
            {
                return args.Length > 0 ? string.Format(fallback, args) : fallback;
            }

            return key; // Return key as last resort
        }

        private string GetCurrentLanguage()
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            if (request == null) return DefaultLang;

            // Check Accept-Language header (e.g. "vi", "en", "ja")
            var acceptLang = request.Headers.AcceptLanguage.FirstOrDefault();

            if (!string.IsNullOrEmpty(acceptLang))
            {
                // Take first 2 chars (e.g. "vi-VN" → "vi")
                var lang = acceptLang.Split(',', '-')[0].Trim().ToLower();
                if (SupportedLangs.Contains(lang))
                {
                    return lang;
                }
            }

            return DefaultLang;
        }
    }
}
