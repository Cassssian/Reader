using SQLite;

namespace Reader.Models;

// Un roman sauvegardé. SQLite local + sync optionnelle Supabase.
public class Webnovel
{
    [PrimaryKey] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Url { get; set; } = "";           // page d'accueil du roman
    public string Title { get; set; } = "";
    public string Author { get; set; } = "";
    public string Cover { get; set; } = "";          // URL scrapée
    public string CustomCover { get; set; } = "";    // chemin local ou URL Supabase Storage
    public string Desc { get; set; } = "";
    public int EpCount { get; set; }
    public int LastEp { get; set; }
    public double Progress { get; set; }             // 0..1 sur l'ensemble du roman
    public long Updated { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public bool Dirty { get; set; } = true;          // en attente de sync Supabase

    [Ignore] public string DisplayCover => string.IsNullOrEmpty(CustomCover) ? Cover : CustomCover;
    [Ignore] public string ProgressLabel => $"{(int)(Progress * 100)}%";
}
