using System.Net.Http.Json;
using System.Web;

namespace Reader.Services;

// MyMemory: totalement gratuit, aucune clé requise (5 000 car/jour par IP).
// Avec adresse email (optionnel) : 50 000 car/jour. Toujours 0 €.
// Doc : https://mymemory.translated.net/doc/spec.php
public class TranslationService
{
    const string Base = "https://api.mymemory.translated.net/get";

    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    readonly Dictionary<string, string> _cache = new();

    // Optionnel : enregistrer son email sur mymemory.translated.net pour passer à 50k/jour.
    public string? Email { get; set; }

    public static readonly (string Code, string Name)[] Languages =
    {
        ("en", "English"), ("fr", "Français"), ("es", "Español"), ("de", "Deutsch"),
        ("it", "Italiano"), ("pt", "Português"), ("ru", "Русский"), ("ja", "日本語"),
        ("ko", "한국어"), ("zh", "中文"), ("ar", "العربية"), ("hi", "हिन्दी")
    };

    public async Task<string> Translate(string text, string target, string source = "en")
    {
        if (string.IsNullOrWhiteSpace(text) || target == source) return text;
        var key = $"{target}:{text.GetHashCode()}";
        if (_cache.TryGetValue(key, out var hit)) return hit;
        try
        {
            // MyMemory limite chaque requête à ~500 chars ; on découpe par paragraphe.
            var parts = text.Split("\n\n");
            var outp = new string[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                outp[i] = await One(parts[i], source, target);
            var res = string.Join("\n\n", outp);
            _cache[key] = res;
            return res;
        }
        catch { return text; }  // hors ligne ou quota -> texte original
    }

    async Task<string> One(string q, string src, string tgt)
    {
        if (string.IsNullOrWhiteSpace(q)) return q;
        var pair = $"{src}|{tgt}";
        var email = Email is not null ? $"&de={HttpUtility.UrlEncode(Email)}" : "";
        var url = $"{Base}?q={HttpUtility.UrlEncode(q)}&langpair={pair}{email}";
        var r = await _http.GetFromJsonAsync<MyMemoryResp>(url);
        return r?.responseData?.translatedText ?? q;
    }

    record TranslatedData(string translatedText);
    record MyMemoryResp(TranslatedData? responseData);
}
