namespace Reader.Services;

// Word boundary callback: char offset + length inside the utterance currently spoken.
// The reader uses it to highlight the live word/sentence.
public interface ITtsService
{
    event Action<int, int>? Word;   // (start, length)
    event Action? Done;
    bool IsPlaying { get; }
    Task Speak(string text, double rate, string lang);
    void Pause();
    void Resume();
    void Stop();
}

// Single source file, platform bodies selected via #if. Each native engine exposes
// word-range callbacks which we normalise into the Word event. A storyteller feel comes
// from a slightly lowered pitch + the source punctuation (commas/periods => natural pauses).
public partial class TtsService : ITtsService
{
    public event Action<int, int>? Word;
    public event Action? Done;
    public bool IsPlaying { get; private set; }

    public static ITtsService Create() => new TtsService();

    void RaiseWord(int s, int l) => MainThread.BeginInvokeOnMainThread(() => Word?.Invoke(s, l));
    void RaiseDone() { IsPlaying = false; MainThread.BeginInvokeOnMainThread(() => Done?.Invoke()); }

#if ANDROID
    Android.Speech.Tts.TextToSpeech? _tts;

    public async Task Speak(string text, double rate, string lang)
    {
        _tts ??= await Init();
        _tts!.SetSpeechRate((float)rate);
        _tts.SetPitch(0.95f);                       // storyteller: a touch below neutral
        // ForLanguageTag remplace new Locale(string), obsolète depuis Android 36.
        _tts.SetLanguage(Java.Util.Locale.ForLanguageTag(lang));
        IsPlaying = true;
        // QueueFlush replaces anything pending; "u" id ties callbacks to this utterance.
        _tts.Speak(text, Android.Speech.Tts.QueueMode.Flush, null, "u");
    }

    Task<Android.Speech.Tts.TextToSpeech> Init()
    {
        var tcs = new TaskCompletionSource<Android.Speech.Tts.TextToSpeech>();
        Android.Speech.Tts.TextToSpeech tts = null!;
        tts = new Android.Speech.Tts.TextToSpeech(Android.App.Application.Context,
            new Listener(() => tcs.TrySetResult(tts)));
        tts.SetOnUtteranceProgressListener(new Progress(RaiseWord, RaiseDone));
        return tcs.Task;
    }

    public void Pause() => Stop();                  // Android engine has no true pause
    public void Resume() { }
    public void Stop() { _tts?.Stop(); IsPlaying = false; }

    class Listener(Action cb) : Java.Lang.Object, Android.Speech.Tts.TextToSpeech.IOnInitListener
    { public void OnInit(Android.Speech.Tts.OperationResult s) => cb(); }

    class Progress(Action<int, int> word, Action done) : Android.Speech.Tts.UtteranceProgressListener
    {
        public override void OnStart(string? id) { }
        public override void OnDone(string? id) => done();
        // OnError(string) est obsolète depuis Android 21 mais doit rester pour la compat API 24+.
        [Obsolete] public override void OnError(string? id) => done();
        public override void OnRangeStart(string? id, int start, int end, int frame) => word(start, end - start);
    }
#elif IOS || MACCATALYST
    readonly AVFoundation.AVSpeechSynthesizer _syn = new();

    public Task Speak(string text, double rate, string lang)
    {
        _syn.Delegate = new Del(RaiseWord, RaiseDone);
        var u = new AVFoundation.AVSpeechUtterance(text)
        {
            Voice = AVFoundation.AVSpeechSynthesisVoice.FromLanguage(lang),
            // Apple rate is 0..1 around a ~0.5 default; map our 0.5..2.0 onto it.
            Rate = (float)(AVFoundation.AVSpeechUtterance.DefaultSpeechRate * rate),
            PitchMultiplier = 0.95f
        };
        IsPlaying = true;
        _syn.SpeakUtterance(u);
        return Task.CompletedTask;
    }

    public void Pause() => _syn.PauseSpeaking(AVFoundation.AVSpeechBoundary.Word);
    public void Resume() => _syn.ContinueSpeaking();
    public void Stop() { _syn.StopSpeaking(AVFoundation.AVSpeechBoundary.Immediate); IsPlaying = false; }

    class Del(Action<int, int> word, Action done) : AVFoundation.AVSpeechSynthesizerDelegate
    {
        public override void WillSpeakRangeOfSpeechString(AVFoundation.AVSpeechSynthesizer s,
            Foundation.NSRange r, AVFoundation.AVSpeechUtterance u) => word((int)r.Location, (int)r.Length);
        public override void DidFinishSpeechUtterance(AVFoundation.AVSpeechSynthesizer s,
            AVFoundation.AVSpeechUtterance u) => done();
    }
#elif WINDOWS
    readonly System.Speech.Synthesis.SpeechSynthesizer _syn = new();

    public Task Speak(string text, double rate, string lang)
    {
        _syn.SpeakAsyncCancelAll();
        _syn.SpeakProgress -= OnProgress; _syn.SpeakCompleted -= OnDone;
        _syn.SpeakProgress += OnProgress; _syn.SpeakCompleted += OnDone;
        // System.Speech Rate is -10..10; map 0.5..2.0 -> roughly -5..10.
        _syn.Rate = (int)Math.Clamp((rate - 1.0) * 10, -10, 10);
        try { _syn.SelectVoiceByHints(System.Speech.Synthesis.VoiceGender.Neutral); } catch { }
        IsPlaying = true;
        _syn.SpeakAsync(text);
        return Task.CompletedTask;
    }

    void OnProgress(object? s, System.Speech.Synthesis.SpeakProgressEventArgs e)
        => RaiseWord(e.CharacterPosition, e.CharacterCount);
    // Ne pas signaler "terminé" sur un Stop() (Cancelled) → éviterait un faux
    // enchaînement de bloc/chapitre.
    void OnDone(object? s, System.Speech.Synthesis.SpeakCompletedEventArgs e)
    { if (!e.Cancelled) RaiseDone(); }

    public void Pause() { _syn.Pause(); IsPlaying = false; }
    public void Resume() { _syn.Resume(); IsPlaying = true; }
    public void Stop() { _syn.SpeakAsyncCancelAll(); IsPlaying = false; }
#else
    // Cross-platform fallback (also the cloud-TTS hook point). No word boundaries.
    public async Task Speak(string text, double rate, string lang)
    {
        IsPlaying = true;
        await Microsoft.Maui.Media.TextToSpeech.Default.SpeakAsync(text,
            new Microsoft.Maui.Media.SpeechOptions { Pitch = 1f });
        RaiseDone();
    }
    public void Pause() { }
    public void Resume() { }
    public void Stop() => IsPlaying = false;
#endif
}
