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

    // ~chars spoken per second at 1x; used to translate the 10s skip + seekbar into offsets.
    const double Cps = 15;

    string _spoken = "";    // the exact text handed to the TTS engine (already translated)
    int _base;              // absolute char offset where the current utterance starts

    public ReaderViewModel(Database db, ITtsService tts, TranslationService tr, WebnovelScraper scraper, SyncService sync)
    {
        (_db, _tts, _tr, _scraper, _sync) = (db, tts, tr, scraper, sync);
        Lang = sync.Settings.Lang; Speed = sync.Settings.Speed; Mode = sync.Settings.Mode;
        _tts.Word += OnWord;
        _tts.Done += OnDone;
    }

    [ObservableProperty] string novelId = "";
    [ObservableProperty] int ep;
    [ObservableProperty] Episode? episode;
    [ObservableProperty] string text = "";
    [ObservableProperty] bool playing;
    [ObservableProperty] double position;           // 0..1 for the seekbar
    [ObservableProperty] int hlStart;                // current word offset within Text
    [ObservableProperty] int hlLen;
    [ObservableProperty] int mode;                   // 0 audio · 1 audio+text · 2 text
    [ObservableProperty] string lang;
    [ObservableProperty] double speed;

    public (string Code, string Name)[] Languages => TranslationService.Languages;
    public bool ShowText => Mode != 0;
    public bool ShowAudio => Mode != 2;
    // Le générateur CommunityToolkit impose "value" comme nom de paramètre dans les partiels.
    partial void OnModeChanged(int value) { OnPropertyChanged(nameof(ShowText)); OnPropertyChanged(nameof(ShowAudio)); }
    partial void OnEpChanged(int value) => _ = Load();
    partial void OnSpeedChanged(double value)
    {
        if (Playing) { _tts.Stop(); _ = SpeakFrom(_base); }
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

        // Lazy fetch + cache the chapter body for offline re-reads.
        if (!Episode.Cached)
        {
            Episode.Content = await _scraper.FetchContent(Episode.Url);
            await _db.Save(Episode);
        }
        await Render();
        Busy = false;
    }

    // Translate (cached) then expose for display + speech.
    async Task Render()
    {
        var src = Episode?.Content ?? "";
        _spoken = await _tr.Translate(src, Lang);
        Text = _spoken;
        ResetHl();
    }

    // --- transport ---
    [RelayCommand]
    async Task PlayPause()
    {
        if (Playing) { _tts.Pause(); Playing = false; await SaveProgress(); }
        else { Playing = true; await SpeakFrom(_base); }
    }

    [RelayCommand] void Forward() => Skip(10);
    [RelayCommand] void Back() => Skip(-10);

    [RelayCommand]
    async Task ChangeLang(string code)
    {
        Lang = code;
        var was = Playing; _tts.Stop(); Playing = false;
        await Render();
        await _sync.SaveReaderPrefs(Lang, Speed, Mode);
        if (was) await PlayPause();
    }


    // Seekbar scrub: jump to a fraction of the text and resume from there.
    public void SeekTo(double frac)
    {
        _tts.Stop();
        _ = SpeakFrom((int)(Math.Clamp(frac, 0, 1) * _spoken.Length));
    }

    void Skip(int seconds)
    {
        var delta = (int)(seconds * Cps * Speed);
        _tts.Stop();
        _ = SpeakFrom(Math.Clamp(_base + delta, 0, Math.Max(0, _spoken.Length - 1)));
    }

    Task SpeakFrom(int offset)
    {
        _base = Math.Clamp(offset, 0, Math.Max(0, _spoken.Length));
        Playing = true;
        return Mode == 2 ? Task.CompletedTask : _tts.Speak(_spoken[_base..], Speed, Lang);
    }

    // --- engine callbacks ---
    void OnWord(int start, int len)
    {
        var abs = _base + start;                     // engine offsets are relative to the substring
        HlStart = abs; HlLen = len;
        if (_spoken.Length > 0) Position = (double)abs / _spoken.Length;
    }

    void OnDone()
    {
        Playing = false;
        _ = NextOrFinish();
    }

    async Task NextOrFinish()
    {
        await SaveProgress(done: true);
        var next = await _db.EpisodeAt(NovelId, Ep + 1);
        if (next is not null) { Ep += 1; }           // auto-advance to next chapter
    }

    void ResetHl() { HlStart = HlLen = 0; Position = 0; _base = 0; }

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
        // Detach so this transient VM isn't kept alive by the singleton TTS service.
        _tts.Word -= OnWord;
        _tts.Done -= OnDone;
        _ = SaveProgress();
    }
}
