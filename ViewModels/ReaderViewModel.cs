using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reader.Models;
using Reader.Services;

namespace Reader.ViewModels;

[QueryProperty(nameof(NovelId), "id")]
[QueryProperty(nameof(Ep), "ep")]
public partial class ReaderViewModel : BaseViewModel
{
    readonly Database _db;
    readonly ITtsService _tts;
    readonly TranslationService _tr;
    readonly WebnovelScraper _scraper;
    readonly SyncService _sync;

    // Les moteurs TTS natifs rejettent silencieusement les textes trop longs
    // (Android ~4000 car). On découpe donc en blocs et on les lit en séquence.
    const int ChunkSize = 2500;
    const double Cps = 15;   // ~caractères/seconde à 1x → conversion du skip 10s

    List<string> _chunks = new();   // blocs successifs ; concaténés == Text
    List<int> _starts = new();      // offset absolu de chaque bloc dans Text
    int _ci;                        // index du bloc en cours
    int _abs;                       // offset absolu du dernier mot prononcé
    int _uStart;                    // offset absolu où commence l'énoncé TTS courant
    bool _continuePlay;             // enchaîne automatiquement le chapitre suivant

    public ReaderViewModel(Database db, ITtsService tts, TranslationService tr, WebnovelScraper scraper, SyncService sync)
    {
        (_db, _tts, _tr, _scraper, _sync) = (db, tts, tr, scraper, sync);
        Lang = sync.Settings.Lang; Speed = sync.Settings.Speed; Mode = sync.Settings.Mode;
        _tts.Word += OnWord;
        _tts.Done += OnChunkDone;
    }

    [ObservableProperty] string novelId = "";
    [ObservableProperty] int ep;
    [ObservableProperty] Episode? episode;
    [ObservableProperty] string text = "";
    [ObservableProperty] bool playing;
    [ObservableProperty] bool translating;          // overlay de chargement
    [ObservableProperty] double position;
    [ObservableProperty] int hlStart;
    [ObservableProperty] int hlLen;
    [ObservableProperty] int mode;
    [ObservableProperty] string lang;
    [ObservableProperty] double speed;

    public (string Code, string Name)[] Languages => TranslationService.Languages;
    public bool ShowText => Mode != 0;
    public bool ShowAudio => Mode != 2;

    partial void OnModeChanged(int value) { OnPropertyChanged(nameof(ShowText)); OnPropertyChanged(nameof(ShowAudio)); }
    partial void OnEpChanged(int value) => _ = Load();
    partial void OnSpeedChanged(double value)
    {
        if (Playing) { _tts.Stop(); StartAt(_abs); }   // relance au nouveau débit
        _ = _sync.SaveReaderPrefs(Lang, value, Mode);
    }
    partial void OnModeChanging(int oldValue, int newValue)
    {
        if (newValue == 2 && Playing) { _tts.Stop(); Playing = false; }
    }

    async Task Load()
    {
        Busy = true;
        Episode = await _db.EpisodeAt(NovelId, Ep);
        if (Episode is null) { Busy = false; return; }

        // Récupère + cache le texte du chapitre (lecture hors ligne ensuite).
        if (!Episode.Cached)
        {
            try { Episode.Content = await _scraper.FetchContent(Episode.Url); }
            catch { /* hors ligne */ }
            if (Episode.Cached) await _db.Save(Episode);
        }
        await Render();
        // Reprend là où on s'était arrêté dans le chapitre.
        _abs = (int)(Episode.Progress * Math.Max(0, Text.Length));
        Position = Episode.Progress;
        Busy = false;

        // Enchaînement automatique depuis le chapitre précédent.
        if (_continuePlay) { _continuePlay = false; StartAt(0); }
    }

    // Traduit (avec overlay) puis prépare l'affichage + le découpage TTS.
    async Task Render()
    {
        var src = Episode?.Content ?? "";
        if (string.IsNullOrWhiteSpace(src)) { Text = "Chapitre indisponible (impossible de récupérer le texte)."; Prepare(); return; }

        Translating = true;
        try { Text = await _tr.Translate(src, Lang); }
        catch { Text = src; }
        Translating = false;
        Prepare();
    }

    // Découpe Text en blocs (sans perdre de caractères → les offsets de surlignage
    // correspondent au Text affiché).
    void Prepare()
    {
        _chunks = new(); _starts = new();
        int i = 0;
        while (i < Text.Length)
        {
            int len = Math.Min(ChunkSize, Text.Length - i);
            int end = i + len;
            if (end < Text.Length)
            {
                int br = Text.LastIndexOfAny(new[] { '.', '!', '?', '\n', ' ' }, end - 1, len);
                if (br > i) end = br + 1;
            }
            _starts.Add(i);
            _chunks.Add(Text[i..end]);
            i = end;
        }
        ResetHl();
    }

    // --- transport ---
    [RelayCommand]
    async Task PlayPause()
    {
        if (Playing) { _tts.Pause(); Playing = false; await SaveProgress(); }
        else if (_chunks.Count > 0) StartAt(_abs);
    }

    [RelayCommand] void Forward() => Skip(10);
    [RelayCommand] void Back() => Skip(-10);

    [RelayCommand]
    async Task ChangeLang(string code)
    {
        if (code == Lang) return;
        Lang = code;
        var was = Playing; _tts.Stop(); Playing = false;
        await Render();
        await _sync.SaveReaderPrefs(Lang, Speed, Mode);
        if (was) StartAt(_abs);
    }

    public void SeekTo(double frac) => StartAt((int)(Math.Clamp(frac, 0, 1) * Text.Length));

    void Skip(int seconds)
    {
        var target = Math.Clamp(_abs + (int)(seconds * Cps * Speed), 0, Math.Max(0, Text.Length - 1));
        StartAt(target);
    }

    // Démarre/relance la lecture à un offset absolu : trouve le bon bloc et parle
    // depuis l'offset interne.
    void StartAt(int abs)
    {
        _tts.Stop();
        if (Mode == 2 || _chunks.Count == 0) return;
        abs = Math.Clamp(abs, 0, Math.Max(0, Text.Length - 1));
        _ci = ChunkOf(abs);
        _abs = _uStart = abs;
        int within = abs - _starts[_ci];
        Playing = true;
        _ = _tts.Speak(_chunks[_ci][within..], Speed, Lang);
    }

    int ChunkOf(int abs)
    {
        for (int k = _starts.Count - 1; k >= 0; k--)
            if (abs >= _starts[k]) return k;
        return 0;
    }

    // --- callbacks moteur ---
    void OnWord(int start, int len)
    {
        _abs = _uStart + start;          // offsets du moteur relatifs à l'énoncé envoyé
        HlStart = _abs; HlLen = len;
        if (Text.Length > 0) Position = (double)_abs / Text.Length;
    }

    // Un bloc est terminé → bloc suivant, sinon chapitre suivant.
    void OnChunkDone()
    {
        if (!Playing) return;
        if (_ci + 1 < _chunks.Count)
        {
            _ci++;
            _abs = _uStart = _starts[_ci];   // énoncé suivant = bloc entier
            _ = _tts.Speak(_chunks[_ci], Speed, Lang);
        }
        else { _ = NextEpisode(); }
    }

    async Task NextEpisode()
    {
        await SaveProgress(done: true);
        var next = await _db.EpisodeAt(NovelId, Ep + 1);
        if (next is not null) { _continuePlay = true; Ep += 1; }  // OnEpChanged → Load → auto-play
        else Playing = false;
    }

    void ResetHl() { HlStart = HlLen = 0; }

    async Task SaveProgress(bool done = false)
    {
        if (Episode is null) return;
        Episode.Progress = done ? 1 : Position;
        await _db.Save(Episode);
        var novel = await _db.Novel(NovelId);
        if (novel is not null)
        {
            novel.LastEp = Ep;
            novel.Progress = novel.EpCount > 0 ? (Ep + Episode.Progress) / novel.EpCount : 0;
            await _sync.Push(novel);
        }
    }

    public void Leave()
    {
        _tts.Stop();
        Playing = false;
        _tts.Word -= OnWord;
        _tts.Done -= OnChunkDone;
        _ = SaveProgress();
    }
}
