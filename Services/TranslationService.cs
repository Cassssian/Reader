using System.Text;
using System.Text.Json;

namespace Reader.Services;

// Traduction gratuite via l'endpoint public Google (gtx) — sans clé, sans CB,
// limites très larges (vs MyMemory ~5000 car/jour qui était insuffisant pour un
// chapitre entier). Fallback MyMemory si Google échoue. Cache par (source,cible,texte).
public class TranslationService
{
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(40) };
    readonly Dictionary<string, string> _cache = new();

    public static readonly (string Code, string Name)[] Languages =
    {
        ("en", "English"), ("fr", "Français"), ("es", "Español"), ("de", "Deutsch"),
        ("it", "Italiano"), ("pt", "Português"), ("ru", "Русский"), ("ja", "日本語"),
        ("ko", "한국어"), ("zh", "中文"), ("ar", "العربية"), ("hi", "हिन्दी")
    };

    // Auto-détection de la langue source par défaut.
    public async Task<string> Translate(string text, string target, string source = "auto")
    {
        if (string.IsNullOrWhiteSpace(text) || target == source) return text;
        var key = $"{source}:{target}:{text.GetHashCode()}";
        if (_cache.TryGetValue(key, out var hit)) return hit;

        try
        {
            // Traduction paragraphe par paragraphe → préserve les sauts de ligne
            // (qui structurent l'affichage et le découpage TTS).
            var paras = text.Split("\n\n");
            var outp = new string[paras.Length];
            for (int i = 0; i < paras.Length; i++)
                outp[i] = await TransPara(paras[i], source, target);
            var res = string.Join("\n\n", outp);
            _cache[key] = res;
            return res;
        }
        catch { return text; }  // hors ligne → texte original
    }

    // Un paragraphe : découpé en sous-blocs <1800 car (limite d'URL GET) si nécessaire.
    async Task<string> TransPara(string para, string src, string tgt)
    {
        if (string.IsNullOrWhiteSpace(para)) return para;
        if (para.Length <= 1800) return await Google(para, src, tgt);

        var sb = new StringBuilder();
        foreach (var piece in SplitSentences(para, 1800))
            sb.Append(await Google(piece, src, tgt));
        return sb.ToString();
    }

    // Endpoint gtx : renvoie un tableau JSON imbriqué [[[trad,orig,...],...],...].
    async Task<string> Google(string q, string sl, string tl)
    {
        var url = $"https://translate.googleapis.com/translate_a/single?client=gtx" +
                  $"&sl={sl}&tl={tl}&dt=t&q={Uri.EscapeDataString(q)}";
        try
        {
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(url));
            var sb = new StringBuilder();
            foreach (var seg in doc.RootElement[0].EnumerateArray())
                sb.Append(seg[0].GetString());
            return sb.ToString();
        }
        catch { return await MyMemory(q, tl); }   // fallback gratuit
    }

    // Fallback : MyMemory (gratuit, sans clé).
    async Task<string> MyMemory(string q, string tgt)
    {
        try
        {
            var url = $"https://api.mymemory.translated.net/get?q={Uri.EscapeDataString(q)}&langpair=en|{tgt}";
            using var doc = JsonDocument.Parse(await _http.GetStringAsync(url));
            return doc.RootElement.GetProperty("responseData").GetProperty("translatedText").GetString() ?? q;
        }
        catch { return q; }
    }

    // Découpe un long texte en blocs <=max sans perdre de caractères, en coupant
    // de préférence après une fin de phrase.
    static IEnumerable<string> SplitSentences(string t, int max)
    {
        int i = 0;
        while (i < t.Length)
        {
            int len = Math.Min(max, t.Length - i);
            int end = i + len;
            if (end < t.Length)
            {
                int br = t.LastIndexOfAny(new[] { '.', '!', '?', '\n' }, end - 1, len);
                if (br > i) end = br + 1;
            }
            yield return t[i..end];
            i = end;
        }
    }
}
