using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Reader.Models;

namespace Reader.Services;

// REST-based Firebase client (no native SDKs => one codebase, no per-platform google
// config wiring beyond the OAuth client ids). Google sign-in via WebAuthenticator, then
// Firestore for data and Storage for custom covers. See FIREBASE_SETUP.md.
public class FirebaseService
{
    // ---- Fill from your Firebase project (see FIREBASE_SETUP.md) ----
    const string ApiKey = "YOUR_WEB_API_KEY";
    const string ProjectId = "YOUR_PROJECT_ID";
    const string Bucket = "YOUR_PROJECT_ID.appspot.com";
    const string GoogleClientId = "YOUR_OAUTH_CLIENT_ID.apps.googleusercontent.com";
    static readonly Uri Redirect = new("readerapp://auth");

    readonly HttpClient _http = new();
    static readonly JsonSerializerOptions J = new() { PropertyNameCaseInsensitive = true };

    public string? Uid { get; private set; }
    public string? Token { get; private set; }
    public bool SignedIn => Token is not null;

    string Docs => $"https://firestore.googleapis.com/v1/projects/{ProjectId}/databases/(default)/documents";

    // --- AUTH: Google OAuth -> Firebase identity token ---
    public async Task<bool> SignIn()
    {
        try
        {
            var auth = await WebAuthenticator.Default.AuthenticateAsync(
                new Uri($"https://accounts.google.com/o/oauth2/v2/auth?client_id={GoogleClientId}" +
                        $"&redirect_uri={Redirect}&response_type=id_token&scope=openid%20email%20profile&nonce={Guid.NewGuid():N}"),
                Redirect);

            var googleIdToken = auth.IdToken ?? auth.Properties["id_token"];
            // Exchange the Google token for a Firebase session token.
            var r = await _http.PostAsJsonAsync(
                $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key={ApiKey}",
                new { postBody = $"id_token={googleIdToken}&providerId=google.com", requestUri = Redirect.ToString(), returnSecureToken = true });
            var d = await r.Content.ReadFromJsonAsync<JsonElement>();
            Token = d.GetProperty("idToken").GetString();
            Uid = d.GetProperty("localId").GetString();
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            return true;
        }
        catch { return false; }
    }

    public void SignOut() { Token = Uid = null; _http.DefaultRequestHeaders.Authorization = null; }

    // --- FIRESTORE: novels collection ---
    public async Task PushNovel(Webnovel n)
    {
        if (!SignedIn) return;
        var body = new { fields = Fields(n) };
        await _http.PatchAsync($"{Docs}/users/{Uid}/novels/{n.Id}", JsonBody(body));
    }

    public async Task<List<Webnovel>> PullNovels()
    {
        var list = new List<Webnovel>();
        if (!SignedIn) return list;
        var r = await _http.GetAsync($"{Docs}/users/{Uid}/novels?pageSize=300");
        if (!r.IsSuccessStatusCode) return list;
        var d = await r.Content.ReadFromJsonAsync<JsonElement>();
        if (!d.TryGetProperty("documents", out var docs)) return list;
        foreach (var doc in docs.EnumerateArray())
        {
            var n = ToNovel(doc.GetProperty("fields"));
            n.Id = doc.GetProperty("name").GetString()!.Split('/')[^1];   // id = last path segment
            list.Add(n);
        }
        return list;
    }

    // --- FIRESTORE: settings doc ---
    public async Task PushSettings(UserSettings s)
    {
        if (!SignedIn) return;
        var body = new { fields = new Dictionary<string, object>
        {
            ["dark"] = new { booleanValue = s.Dark },
            ["lang"] = new { stringValue = s.Lang },
            ["speed"] = new { doubleValue = s.Speed },
            ["mode"] = new { integerValue = s.Mode.ToString() },
            ["updated"] = new { integerValue = s.Updated.ToString() }
        }};
        await _http.PatchAsync($"{Docs}/users/{Uid}/meta/settings", JsonBody(body));
    }

    public async Task<UserSettings?> PullSettings()
    {
        if (!SignedIn) return null;
        var r = await _http.GetAsync($"{Docs}/users/{Uid}/meta/settings");
        if (!r.IsSuccessStatusCode) return null;
        var d = await r.Content.ReadFromJsonAsync<JsonElement>();
        if (!d.TryGetProperty("fields", out var f)) return null;
        return new UserSettings
        {
            Dark = Bool(f, "dark"), Lang = Str(f, "lang"),
            Speed = Dbl(f, "speed"), Mode = (int)Int(f, "mode"), Updated = Int(f, "updated")
        };
    }

    // --- STORAGE: upload a custom cover, return its public download url ---
    public async Task<string> UploadCover(string novelId, Stream img)
    {
        if (!SignedIn) return "";
        var name = Uri.EscapeDataString($"covers/{Uid}/{novelId}.jpg");
        var content = new StreamContent(img);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var r = await _http.PostAsync($"https://firebasestorage.googleapis.com/v0/b/{Bucket}/o?name={name}", content);
        var d = await r.Content.ReadFromJsonAsync<JsonElement>();
        var tok = d.GetProperty("downloadTokens").GetString();
        return $"https://firebasestorage.googleapis.com/v0/b/{Bucket}/o/{name}?alt=media&token={tok}";
    }

    // --- Firestore <-> model mapping (typed-value JSON) ---
    static Dictionary<string, object> Fields(Webnovel n) => new()
    {
        ["url"] = new { stringValue = n.Url },
        ["title"] = new { stringValue = n.Title },
        ["author"] = new { stringValue = n.Author },
        ["cover"] = new { stringValue = n.Cover },
        ["customCover"] = new { stringValue = n.CustomCover },
        ["desc"] = new { stringValue = n.Desc },
        ["epCount"] = new { integerValue = n.EpCount.ToString() },
        ["lastEp"] = new { integerValue = n.LastEp.ToString() },
        ["progress"] = new { doubleValue = n.Progress },
        ["updated"] = new { integerValue = n.Updated.ToString() }
    };

    static Webnovel ToNovel(JsonElement f) => new()
    {
        Url = Str(f, "url"), Title = Str(f, "title"), Author = Str(f, "author"),
        Cover = Str(f, "cover"), CustomCover = Str(f, "customCover"), Desc = Str(f, "desc"),
        EpCount = (int)Int(f, "epCount"), LastEp = (int)Int(f, "lastEp"),
        Progress = Dbl(f, "progress"), Updated = Int(f, "updated"), Dirty = false
    };

    static string Str(JsonElement f, string k) => f.TryGetProperty(k, out var v) && v.TryGetProperty("stringValue", out var s) ? s.GetString() ?? "" : "";
    static bool Bool(JsonElement f, string k) => f.TryGetProperty(k, out var v) && v.TryGetProperty("booleanValue", out var b) && b.GetBoolean();
    static double Dbl(JsonElement f, string k) => f.TryGetProperty(k, out var v) && v.TryGetProperty("doubleValue", out var d) ? d.GetDouble() : 0;
    static long Int(JsonElement f, string k) => f.TryGetProperty(k, out var v) && v.TryGetProperty("integerValue", out var i) && long.TryParse(i.GetString(), out var n) ? n : 0;

    static StringContent JsonBody(object o) => new(JsonSerializer.Serialize(o), System.Text.Encoding.UTF8, "application/json");
}
