using System.Net.Http.Json;

namespace Reader.Services;

// Free LibreTranslate backend. Auto-detects source ("auto") and caches per
// (text,lang) so re-opening a chapter or switching modes never re-hits the API.
public class TranslationService
{
    // Public mirror; swap for a self-hosted instance + ApiKey in production.
    const string Endpoint = "https://libretranslate.com/translate";

    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
    readonly Dictionary<string, string> _cache = new();

    public string? ApiKey { get; set; }

    public static readonly (string Code, string Name)[] Languages =
    {
        ("en", "English"), ("fr", "Français"), ("es", "Español"), ("de", "Deutsch"),
        ("it", "Italiano"), ("pt", "Português"), ("ru", "Русский"), ("ja", "日本語"),
        ("ko", "한국어"), ("zh", "中文"), ("ar", "العربية"), ("hi", "हिन्दी")
    };

    public async Task<string> Translate(string text, string target, string source = "auto")
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var key = $"{target}:{text.GetHashCode()}";
        if (_cache.TryGetValue(key, out var hit)) return hit;

        try
        {
            // LibreTranslate caps payload size; translate per-paragraph to stay safe
            // and to keep word/sentence offsets aligned for the reader highlight.
            var parts = text.Split("\n\n");
            var outp = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                outp[i] = await One(parts[i], target, source);
            var res = string.Join("\n\n", outp);
            _cache[key] = res;
            return res;
        }
        catch { return text; } // offline / API down -> show original
    }

    async Task<string> One(string q, string target, string source)
    {
        if (string.IsNullOrWhiteSpace(q)) return q;
        var resp = await _http.PostAsJsonAsync(Endpoint, new
        {
            q, source, target, format = "text", api_key = ApiKey ?? ""
        });
        resp.EnsureSuccessStatusCode();
        var data = await resp.Content.ReadFromJsonAsync<Resp>();
        return data?.translatedText ?? q;
    }

    record Resp(string translatedText);
}
