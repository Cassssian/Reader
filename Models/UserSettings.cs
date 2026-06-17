namespace Reader.Models;

// Lightweight prefs synced to Firestore (users/{uid}/settings). Theme + reader defaults.
public class UserSettings
{
    public bool Dark { get; set; } = true;
    public string Lang { get; set; } = "en";        // target language code
    public double Speed { get; set; } = 1.0;          // TTS rate 0.5..2.0
    public int Mode { get; set; } = 1;                // 0 audio, 1 audio+text, 2 text
    public long Updated { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
