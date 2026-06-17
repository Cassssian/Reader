using SQLite;

namespace Reader.Models;

// One saved webnovel. Stored locally in SQLite and mirrored to Firestore.
public class Webnovel
{
    [PrimaryKey] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Url { get; set; } = "";          // novel landing page
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Cover { get; set; } = "";         // remote cover or Firebase Storage url
    public string CustomCover { get; set; } = "";   // user override, wins over Cover when set
    public string Desc { get; set; } = "";
    public int EpCount { get; set; }
    public int LastEp { get; set; }                 // index of last opened episode
    public double Progress { get; set; }            // 0..1 across the whole novel
    public long Updated { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public bool Dirty { get; set; } = true;         // needs push to Firestore

    [Ignore] public string DisplayCover => string.IsNullOrEmpty(CustomCover) ? Cover : CustomCover;
    [Ignore] public string ProgressLabel => $"{(int)(Progress * 100)}%";
}
