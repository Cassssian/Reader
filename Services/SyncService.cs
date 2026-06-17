using Reader.Models;

namespace Reader.Services;

// Reconciles local SQLite <-> Firestore using last-write-wins on the Updated timestamp.
// Theme/settings live in Preferences for instant startup and mirror to Firestore.
public class SyncService(Database db, FirebaseService fb)
{
    public UserSettings Settings { get; private set; } = Load();

    public async Task InitAsync()
    {
        // Silent re-auth would go here; for now sync only runs once signed in.
        if (fb.SignedIn) await Pull();
    }

    public async Task<bool> SignIn()
    {
        if (!await fb.SignIn()) return false;
        await Pull();
        return true;
    }

    // Merge remote into local (newer wins), then push anything still dirty.
    public async Task Pull()
    {
        var remote = await fb.PullNovels();
        foreach (var r in remote)
        {
            var local = await db.Novel(r.Id);
            if (local is null || r.Updated > local.Updated) await db.Save(r);
        }
        foreach (var n in await db.Novels())
            if (n.Dirty) { await fb.PushNovel(n); n.Dirty = false; await db.Save(n); }

        var s = await fb.PullSettings();
        if (s is not null && s.Updated > Settings.Updated) Apply(s);
    }

    public async Task Push(Webnovel n)
    {
        n.Dirty = true;
        await db.Save(n);
        if (fb.SignedIn) { await fb.PushNovel(n); n.Dirty = false; await db.Save(n); }
    }

    // --- Settings / theme ---
    public async Task SetTheme(bool dark)
    {
        Settings.Dark = dark;
        Bump();
        Application.Current!.UserAppTheme = dark ? AppTheme.Dark : AppTheme.Light;
        Preferences.Set("dark", dark);
        if (fb.SignedIn) await fb.PushSettings(Settings);
    }

    public async Task SaveReaderPrefs(string lang, double speed, int mode)
    {
        (Settings.Lang, Settings.Speed, Settings.Mode) = (lang, speed, mode);
        Bump();
        Preferences.Set("lang", lang); Preferences.Set("speed", speed); Preferences.Set("mode", mode);
        if (fb.SignedIn) await fb.PushSettings(Settings);
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
        Dark = Preferences.Get("dark", true),
        Lang = Preferences.Get("lang", "en"),
        Speed = Preferences.Get("speed", 1.0),
        Mode = Preferences.Get("mode", 1)
    };
}
