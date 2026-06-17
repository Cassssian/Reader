using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Reader.Models;

namespace Reader.Services;

// Client REST Supabase — 100 % gratuit (free tier, sans CB).
// Inscription : https://supabase.com → New Project → plan Free.
// Récupérer URL + anon key : Settings → API.
// Voir SUPABASE_SETUP.md pour les tables SQL et les règles RLS.
public class SupabaseService
{
    // ----- Remplir avec les valeurs du projet Supabase -----
    public const string Url = "https://VOTRE_REF.supabase.co";   // ex. https://abcdef.supabase.co
    public const string AnonKey = "VOTRE_ANON_KEY";              // clé publique anon

    readonly HttpClient _http = new() { BaseAddress = new Uri(Url) };

    public string? AccessToken { get; private set; }
    public string? Uid { get; private set; }
    public bool SignedIn => AccessToken is not null;

    // --- AUTH : Google OAuth via Supabase (gratuit) ---
    public async Task<bool> SignIn()
    {
        try
        {
            var redirect = new Uri("readerapp://auth");
            // Supabase retourne un URL d'autorisation Google ; WebAuthenticator le gère.
            var authUrl = new Uri($"{Url}/auth/v1/authorize?provider=google&redirect_to={redirect}");
            var result = await WebAuthenticator.Default.AuthenticateAsync(authUrl, redirect);

            var code = result.Properties.GetValueOrDefault("code")
                    ?? result.Properties.GetValueOrDefault("access_token");
            if (code is null) return false;

            // Échange le code contre une session (access_token + user id).
            var resp = await PostAuth("/auth/v1/token?grant_type=pkce", new { auth_code = code });
            AccessToken = resp.GetProperty("access_token").GetString();
            Uid = resp.GetProperty("user").GetProperty("id").GetString();
            SetAuth();
            return true;
        }
        catch { return false; }
    }

    public void SignOut()
    {
        AccessToken = Uid = null;
        _http.DefaultRequestHeaders.Authorization = null;
    }

    // --- NOVELS ---
    public async Task PushNovel(Webnovel n)
    {
        if (!SignedIn) return;
        var body = JsonSerializer.Serialize(new
        {
            id = n.Id, user_id = Uid, url = n.Url, title = n.Title, author = n.Author,
            cover = n.Cover, custom_cover = n.CustomCover, description = n.Desc,
            ep_count = n.EpCount, last_ep = n.LastEp, progress = n.Progress, updated = n.Updated
        });
        // upsert via Prefer: resolution=merge-duplicates
        var req = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/novels")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Prefer", "resolution=merge-duplicates");
        await _http.SendAsync(req);
    }

    public async Task<List<Webnovel>> PullNovels()
    {
        var list = new List<Webnovel>();
        if (!SignedIn) return list;
        var r = await _http.GetAsync($"/rest/v1/novels?user_id=eq.{Uid}&select=*");
        if (!r.IsSuccessStatusCode) return list;
        var arr = await r.Content.ReadFromJsonAsync<JsonElement[]>();
        if (arr is null) return list;
        foreach (var j in arr) list.Add(Row(j));
        return list;
    }

    // --- SETTINGS ---
    public async Task PushSettings(UserSettings s)
    {
        if (!SignedIn) return;
        var body = JsonSerializer.Serialize(new
        {
            user_id = Uid, dark = s.Dark, lang = s.Lang,
            speed = s.Speed, mode = s.Mode, updated = s.Updated
        });
        var req = new HttpRequestMessage(HttpMethod.Post, "/rest/v1/settings")
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        };
        req.Headers.Add("Prefer", "resolution=merge-duplicates");
        await _http.SendAsync(req);
    }

    public async Task<UserSettings?> PullSettings()
    {
        if (!SignedIn) return null;
        var r = await _http.GetAsync($"/rest/v1/settings?user_id=eq.{Uid}&select=*&limit=1");
        if (!r.IsSuccessStatusCode) return null;
        var arr = await r.Content.ReadFromJsonAsync<JsonElement[]>();
        if (arr is null || arr.Length == 0) return null;
        var j = arr[0];
        return new UserSettings
        {
            Dark = j.TryGetProperty("dark", out var d) && d.GetBoolean(),
            Lang = j.TryGetProperty("lang", out var l) ? l.GetString() ?? "en" : "en",
            Speed = j.TryGetProperty("speed", out var sp) ? sp.GetDouble() : 1.0,
            Mode = j.TryGetProperty("mode", out var m) ? m.GetInt32() : 1,
            Updated = j.TryGetProperty("updated", out var u) ? u.GetInt64() : 0
        };
    }

    // --- STORAGE : couverture personnalisée (bucket "covers", public read) ---
    public async Task<string> UploadCover(string novelId, Stream img)
    {
        if (!SignedIn) return "";
        var path = $"{Uid}/{novelId}.jpg";
        var content = new StreamContent(img);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var r = await _http.PostAsync($"/storage/v1/object/covers/{path}", content);
        if (!r.IsSuccessStatusCode) return "";
        return $"{Url}/storage/v1/object/public/covers/{path}";
    }

    // --- helpers ---
    void SetAuth()
    {
        _http.DefaultRequestHeaders.Clear();
        _http.DefaultRequestHeaders.Add("apikey", AnonKey);
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", AccessToken);
    }

    async Task<JsonElement> PostAuth(string path, object body)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(body),
                System.Text.Encoding.UTF8, "application/json")
        };
        req.Headers.Add("apikey", AnonKey);
        var r = await _http.SendAsync(req);
        r.EnsureSuccessStatusCode();
        return await r.Content.ReadFromJsonAsync<JsonElement>();
    }

    static Webnovel Row(JsonElement j) => new()
    {
        Id          = j.GetProperty("id").GetString()!,
        Url         = j.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
        Title       = j.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
        Author      = j.TryGetProperty("author", out var a) ? a.GetString() ?? "" : "",
        Cover       = j.TryGetProperty("cover", out var c) ? c.GetString() ?? "" : "",
        CustomCover = j.TryGetProperty("custom_cover", out var cc) ? cc.GetString() ?? "" : "",
        Desc        = j.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "",
        EpCount     = j.TryGetProperty("ep_count", out var ec) ? ec.GetInt32() : 0,
        LastEp      = j.TryGetProperty("last_ep", out var le) ? le.GetInt32() : 0,
        Progress    = j.TryGetProperty("progress", out var p) ? p.GetDouble() : 0,
        Updated     = j.TryGetProperty("updated", out var up) ? up.GetInt64() : 0,
        Dirty       = false
    };
}
