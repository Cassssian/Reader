using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Reader.Models;

namespace Reader.Services;

// Parses novel/chapter pages into models. Tuned for freewebnovel.com markup but the
// selectors degrade gracefully (fallbacks via OpenGraph + heuristics) for similar sites.
public partial class WebnovelScraper
{
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    public WebnovelScraper()
    {
        // Many novel sites 403 the default .NET UA; pretend to be a browser.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0 Safari/537.36");
    }

    [GeneratedRegex(@"chapter[-/](\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ChapterNum();

    // Given any episode OR novel url, return a fully-populated novel + its episode list.
    public async Task<(Webnovel novel, List<Episode> eps)> Fetch(string url)
    {
        url = Normalize(url);
        var novelUrl = ToNovelUrl(url);
        var doc = await Load(novelUrl);

        var novel = new Webnovel
        {
            Url = novelUrl,
            Title = Meta(doc, "og:title") ?? Text(doc, "//h1") ?? "Unknown",
            Cover = Abs(novelUrl, Meta(doc, "og:image") ?? Attr(doc, "//div[contains(@class,'pic')]//img", "src")),
            Desc = Clean(Meta(doc, "og:description") ?? Text(doc, "//div[contains(@class,'inner')]") ?? ""),
            Author = Text(doc, "//*[contains(@class,'author')]//a") ?? ""
        };

        var eps = Chapters(doc, novelUrl);
        novel.EpCount = eps.Count;
        foreach (var (e, i) in eps.Select((e, i) => (e, i))) { e.NovelId = novel.Id; e.Index = i; }
        return (novel, eps);
    }

    // Fetch + cache the readable text of one chapter.
    public async Task<string> FetchContent(string chapterUrl)
    {
        var doc = await Load(chapterUrl);
        var node = doc.DocumentNode.SelectSingleNode("//div[@id='article']")
                ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'chapter-content')]")
                ?? doc.DocumentNode.SelectSingleNode("//div[contains(@class,'txt')]");
        if (node is null) return "";
        // Keep paragraph breaks (they drive TTS sentence pacing) but drop scripts/ads.
        foreach (var bad in node.SelectNodes(".//script|.//ins|.//div[contains(@class,'ads')]")?.ToArray() ?? Array.Empty<HtmlNode>())
            bad.Remove();
        var paras = node.SelectNodes(".//p")?.Select(p => Clean(p.InnerText)) ?? new[] { Clean(node.InnerText) };
        return string.Join("\n\n", paras.Where(p => p.Length > 0));
    }

    // --- chapter list ---
    static List<Episode> Chapters(HtmlDocument doc, string baseUrl)
    {
        var links = doc.DocumentNode.SelectNodes(
            "//div[contains(@class,'chapter')]//a[@href] | //ul[contains(@id,'idData')]//a[@href] | //div[contains(@id,'chapterList')]//a[@href]");
        var list = new List<Episode>();
        if (links is null) return list;
        var seen = new HashSet<string>();
        foreach (var a in links)
        {
            var href = Abs(baseUrl, a.GetAttributeValue("href", ""));
            if (string.IsNullOrEmpty(href) || !seen.Add(href)) continue;
            list.Add(new Episode { Url = href, Title = Clean(a.InnerText) });
        }
        return list;
    }

    // --- url helpers ---
    static string Normalize(string u)
    {
        u = u.Trim();
        if (!u.StartsWith("http")) u = "https://" + u;
        return u;
    }

    // chapter url -> novel url:  /novel/slug/chapter-573  =>  /novel/slug
    static string ToNovelUrl(string u)
    {
        var m = Regex.Match(u, @"^(https?://[^/]+/novel/[^/]+)", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : u;
    }

    async Task<HtmlDocument> Load(string url)
    {
        var html = await _http.GetStringAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    // --- node extraction helpers ---
    static string? Meta(HtmlDocument d, string prop) =>
        d.DocumentNode.SelectSingleNode($"//meta[@property='{prop}' or @name='{prop}']")
            ?.GetAttributeValue("content", null!);

    static string? Text(HtmlDocument d, string xpath)
    {
        var t = d.DocumentNode.SelectSingleNode(xpath)?.InnerText;
        return string.IsNullOrWhiteSpace(t) ? null : Clean(t);
    }

    static string? Attr(HtmlDocument d, string xpath, string attr) =>
        d.DocumentNode.SelectSingleNode(xpath)?.GetAttributeValue(attr, null!);

    static string Abs(string baseUrl, string? href) =>
        string.IsNullOrEmpty(href) ? "" :
        Uri.TryCreate(new Uri(baseUrl), href, out var abs) ? abs.ToString() : href;

    static string Clean(string s) =>
        Regex.Replace(HtmlEntity.DeEntitize(s ?? "").Replace(' ', ' '), @"\s+", " ").Trim();
}
