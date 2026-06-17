using Reader.Models;

namespace Reader.Services;

// Local-first : SQLite est la source de vérité.
// La sync Supabase est optionnelle (gratuite) et ne bloque jamais le démarrage.
public class SyncService(Database db, SupabaseService supa)
{
    public UserSettings Settings { get; private set; } = Load();

    public async Task InitAsync()
    {
        if (supa.SignedIn) _ = Pull();   // fire-and-forget, non bloquant
    }

    public async Task<bool> SignIn()
    {
        if (!await supa.SignIn()) return false;
        await Pull();
        return true;
    }

    // Merge remote → local (newer wins), puis pousse ce qui est dirty.
    public async Task Pull()
    {
        try
        {
            var remote = await supa.PullNovels();
            foreach (var r in remote)
            {
                var local = await db.Novel(r.Id);
                if (local is null || r.Updated > local.Updated) await db.Save(r);
            }
            foreach (var n in await db.Novels())
                if (n.Dirty) { await supa.PushNovel(n); n.Dirty = false; await db.Save(n); }

            var s = await supa.PullSettings();
            if (s is not null && s.Updated > Settings.Updated) Apply(s);
        }
        catch { /* hors ligne → aucun impact */ }
    }

    public async Task Push(Webnovel n)
    {
        n.Dirty = true;
        await db.Save(n);
        if (supa.SignedIn)
        {
            try { await supa.PushNovel(n); n.Dirty = false; await db.Save(n); }
            catch { /* sera réessayé au prochain Pull */ }
        }
    }

    public async Task SetTheme(bool dark)
    {
        Settings.Dark = dark;
        Bump();
        Application.Current!.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
        Preferences.Set("dark", dark);
        if (supa.SignedIn) _ = supa.PushSettings(Settings);
    }

    public async Task SaveReaderPrefs(string lang, double speed, int mode)
    {
        (Settings.Lang, Settings.Speed, Settings.Mode) = (lang, speed, mode);
        Bump();
        Preferences.Set("lang", lang); Preferences.Set("speed", speed); Preferences.Set("mode", mode);
        if (supa.SignedIn) _ = supa.PushSettings(Settings);
    }

    void Apply(UserSettings s)
    {
        Settings = s;
        Preferences.Set("dark", s.Dark); Preferences.Set("lang", s.Lang);
        Preferences.Set("speed", s.Speed); Preferences.Set("mode", s.Mode);
        Application.Current!.UserAppTheme = s.Dark ? AppTheme.Dark : AppTheme.Light;
    }

    void Bump() => Settings.Updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    static UserSettings Load() => new()
    {
        Dark  = Preferences.Get("dark", true),
        Lang  = Preferences.Get("lang", "en"),
        Speed = Preferences.Get("speed", 1.0),
        Mode  = Preferences.Get("mode", 1)
    };
}
