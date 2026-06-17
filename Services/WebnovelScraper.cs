using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Reader.Models;

namespace Reader.Services;

// Scraper générique multi-sites. Gère freewebnovel, et les thèmes WordPress "Madara"
// (noveltrust.com, /book/...) dont la liste de chapitres est chargée en AJAX.
public partial class WebnovelScraper
{
    readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(25) };

    public WebnovelScraper()
    {
        // Beaucoup de sites renvoient 403 à l'UA .NET par défaut → on simule un navigateur.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0 Safari/537.36");
        _http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
    }

    [GeneratedRegex(@"(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex Num();

    [GeneratedRegex(@"chapter|chap|episode|/ch\d|/c\d|-c\d", RegexOptions.IgnoreCase)]
    private static partial Regex ChapHint();

    // URL chapitre OU roman → roman complet + liste d'épisodes.
    public async Task<(Webnovel novel, List<Episode> eps)> Fetch(string url)
    {
        url = Normalize(url);
        var novelUrl = ToNovelUrl(url);
        var doc = await Load(novelUrl);

        var novel = new Webnovel
        {
            Url = novelUrl,
            Title = Meta(doc, "og:title") ?? Text(doc, "//h1") ?? "Unknown",
            Cover = Abs(novelUrl, Meta(doc, "og:image")
                ?? Attr(doc, "//div[contains(@class,'summary_image')]//img", "src")
                ?? Attr(doc, "//div[contains(@class,'pic')]//img", "src")),
            Desc = Clean(Meta(doc, "og:description")
                ?? Text(doc, "//div[contains(@class,'summary__content')]")
                ?? Text(doc, "//div[contains(@class,'inner')]") ?? ""),
            Author = Text(doc, "//*[contains(@class,'author')]//a")
                ?? Text(doc, "//div[contains(@class,'author-content')]//a") ?? ""
        };

        var eps = await Chapters(doc, novelUrl);
        novel.EpCount = eps.Count;
        for (int i = 0; i < eps.Count; i++) { eps[i].NovelId = novel.Id; eps[i].Index = i; }
        return (novel, eps);
    }

    // Texte lisible d'un chapitre (sélecteurs élargis, multi-thèmes).
    public async Task<string> FetchContent(string chapterUrl)
    {
        var doc = await Load(chapterUrl);
        var node = First(doc,
            "//div[contains(@class,'reading-content')]",   // Madara
            "//div[contains(@class,'text-left')]",
            "//div[contains(@class,'entry-content')]",
            "//div[@id='article']",                          // freewebnovel
            "//div[contains(@class,'chapter-content')]",
            "//div[contains(@class,'chapter-c')]",
            "//div[contains(@class,'txt')]",
            "//article");
        if (node is null) return "";

        foreach (var bad in node.SelectNodes(".//script|.//style|.//ins|.//div[contains(@class,'ad')]")?.ToArray()
                            ?? Array.Empty<HtmlNode>())
            bad.Remove();

        var ps = node.SelectNodes(".//p");
        var paras = ps is not null
            ? ps.Select(p => Clean(p.InnerText)).Where(p => p.Length > 0)
            : new[] { Clean(node.InnerText) };
        return string.Join("\n\n", paras.Where(p => p.Length > 0));
    }

    // --- liste de chapitres ---
    async Task<List<Episode>> Chapters(HtmlDocument doc, string novelUrl)
    {
        var list = Harvest(doc, novelUrl);

        // Thèmes Madara : la liste est vide dans le HTML statique → endpoint AJAX.
        if (list.Count == 0)
            list = await MadaraAjax(novelUrl);

        // Dernier recours : tous les liens "chapitre" de la page.
        if (list.Count == 0)
            list = AnyChapterLinks(doc, novelUrl);

        Order(list);
        return list;
    }

    static List<Episode> Harvest(HtmlDocument doc, string baseUrl) =>
        Collect(doc.DocumentNode.SelectNodes(
            "//li[contains(@class,'wp-manga-chapter')]//a[@href]" +    // Madara
            " | //ul[contains(@class,'chapter')]//a[@href]" +
            " | //div[contains(@class,'eplister')]//a[@href]" +
            " | //div[contains(@class,'chapter')]//a[@href]" +
            " | //ul[contains(@id,'idData')]//a[@href]" +             // freewebnovel
            " | //*[contains(@id,'chapterList')]//a[@href]"), baseUrl);

    // POST {novel}/ajax/chapters/  → fragment HTML (la plupart des Madara récents).
    async Task<List<Episode>> MadaraAjax(string novelUrl)
    {
        try
        {
            var endpoint = novelUrl.TrimEnd('/') + "/ajax/chapters/";
            var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent("", System.Text.Encoding.UTF8)
                {
                    Headers = { ContentType = new MediaTypeHeaderValue("application/x-www-form-urlencoded") }
                }
            };
            req.Headers.Add("X-Requested-With", "XMLHttpRequest");
            var r = await _http.SendAsync(req);
            if (!r.IsSuccessStatusCode) return new();
            var doc = new HtmlDocument();
            doc.LoadHtml(await r.Content.ReadAsStringAsync());
            return Collect(doc.DocumentNode.SelectNodes("//li[contains(@class,'wp-manga-chapter')]//a[@href] | //a[@href]"), novelUrl);
        }
        catch { return new(); }
    }

    static List<Episode> AnyChapterLinks(HtmlDocument doc, string baseUrl)
    {
        var list = new List<Episode>();
        var all = doc.DocumentNode.SelectNodes("//a[@href]");
        if (all is null) return list;
        var seen = new HashSet<string>();
        foreach (var a in all)
        {
            var href = Abs(baseUrl, a.GetAttributeValue("href", ""));
            if (string.IsNullOrEmpty(href) || href == baseUrl || !ChapHint().IsMatch(href)) continue;
            if (!seen.Add(href)) continue;
            list.Add(new Episode { Url = href, Title = Clean(a.InnerText) });
        }
        return list;
    }

    static List<Episode> Collect(HtmlNodeCollection? links, string baseUrl)
    {
        var list = new List<Episode>();
        if (links is null) return list;
        var seen = new HashSet<string>();
        foreach (var a in links)
        {
            var href = Abs(baseUrl, a.GetAttributeValue("href", ""));
            if (string.IsNullOrEmpty(href) || href == baseUrl || !ChapHint().IsMatch(href)) continue;
            if (!seen.Add(href)) continue;
            list.Add(new Episode { Url = href, Title = Clean(a.InnerText) });
        }
        return list;
    }

    // Trie par numéro de chapitre croissant (les sites listent souvent du plus récent
    // au plus ancien). Si aucun numéro détectable, garde l'ordre du DOM.
    static void Order(List<Episode> list)
    {
        bool allNumbered = list.All(e => Num().IsMatch(e.Url) || Num().IsMatch(e.Title));
        if (!allNumbered) return;
        list.Sort((a, b) => ChapKey(a).CompareTo(ChapKey(b)));
    }

    static int ChapKey(Episode e)
    {
        var m = Num().Match(e.Title);
        if (!m.Success) m = Num().Matches(e.Url).LastOrDefault() ?? Match.Empty;
        return m.Success && int.TryParse(m.Value, out var n) ? n : int.MaxValue;
    }

    // --- helpers URL ---
    static string Normalize(string u)
    {
        u = u.Trim();
        if (!u.StartsWith("http")) u = "https://" + u;
        return u;
    }

    // URL chapitre → URL roman : retire un segment de chapitre final s'il y en a un.
    static string ToNovelUrl(string u)
    {
        var uri = new Uri(u);
        var segs = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries).ToList();
        if (segs.Count >= 2 && ChapHint().IsMatch(segs[^1]))
            segs.RemoveAt(segs.Count - 1);
        return $"{uri.Scheme}://{uri.Host}/{string.Join('/', segs)}";
    }

    async Task<HtmlDocument> Load(string url)
    {
        var html = await _http.GetStringAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        return doc;
    }

    // --- helpers extraction ---
    static HtmlNode? First(HtmlDocument d, params string[] xpaths) =>
        xpaths.Select(x => d.DocumentNode.SelectSingleNode(x)).FirstOrDefault(n => n is not null);

    static string? Meta(HtmlDocument d, string prop)
    {
        var c = d.DocumentNode.SelectSingleNode($"//meta[@property='{prop}' or @name='{prop}']")
            ?.GetAttributeValue("content", null!);
        return string.IsNullOrWhiteSpace(c) ? null : c;
    }

    static string? Text(HtmlDocument d, string xpath)
    {
        var t = d.DocumentNode.SelectSingleNode(xpath)?.InnerText;
        return string.IsNullOrWhiteSpace(t) ? null : Clean(t);
    }

    static string? Attr(HtmlDocument d, string xpath, string attr)
    {
        var v = d.DocumentNode.SelectSingleNode(xpath)?.GetAttributeValue(attr, null!);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    static string Abs(string baseUrl, string? href) =>
        string.IsNullOrEmpty(href) ? "" :
        Uri.TryCreate(new Uri(baseUrl), href, out var abs) ? abs.ToString() : href;

    static string Clean(string s) =>
        Regex.Replace(HtmlEntity.DeEntitize(s ?? "").Replace(' ', ' '), @"\s+", " ").Trim();
}
