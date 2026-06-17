using SQLite;

namespace Reader.Models;

// A single chapter. Content is cached lazily on first open (offline support).
public class Episode
{
    [PrimaryKey] public string Id { get; set; } = Guid.NewGuid().ToString("N");
    [Indexed] public string NovelId { get; set; } = "";
    public int Index { get; set; }                  // 0-based order within the novel
    public string Title { get; set; } = "";
    public string Url { get; set; } = "";
    public string Content { get; set; } = "";       // cached plain text, empty until fetched
    public double Progress { get; set; }            // 0..1 within this episode
    public bool Cached => !string.IsNullOrEmpty(Content);

    [Ignore] public string Number => $"#{Index + 1}";
}
