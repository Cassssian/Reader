# Reader

A cross-platform .NET MAUI app (Android · iOS · macOS · Windows) that fetches your
favourite webnovels, reads them aloud with native text-to-speech, and translates
them into any language — with reading progress and theme synced across devices via
Firebase.

## Features
- **Library** — adaptive grid of saved novels with cover, episode count, progress.
  Empty state shows *“Il n'y a rien à écouter ici...”*.
- **Add a novel** — paste a novel **or** chapter URL (e.g.
  `freewebnovel.com/novel/.../chapter-573`), or browse the site in-app and tap
  *Use this page*. Metadata (title, cover, description, chapter list) is scraped
  automatically. Cover can be overridden with a photo (synced to Firebase Storage).
- **Reader** — three modes:
  1. **Audio** — native TTS + animated waveform visualizer + seekbar.
  2. **Audio + Text** — TTS with the spoken word highlighted and auto-scroll.
  3. **Text** — plain reading.
  Plus a language dropdown (auto-translate), 0.5×–2× speed, and ±10s skip.
- **Sync** — novel list, reading progress and theme via Firebase (Firestore +
  Storage + Google sign-in). Offline-first on local SQLite.

## Architecture (MVVM)
```
Models/        Webnovel · Episode · UserSettings
Services/      Database(SQLite) · FirebaseService(REST) · TtsService(per-platform)
               TranslationService(LibreTranslate) · WebnovelScraper(HtmlAgilityPack) · SyncService
ViewModels/    MainMenu · Details · Reader · AddNovel  (CommunityToolkit.Mvvm)
Views/         Splash · MainMenu · Details · Reader · AddNovel
Controls/      EpisodeCard · AudioWaveView · Converters
Platforms/     Android · iOS · MacCatalyst · Windows
```

### Key pieces
- **`TtsService.cs`** — one file, platform bodies via `#if`. Android
  `Android.Speech.Tts`, Apple `AVFoundation`, Windows `System.Speech`, plus a
  `Microsoft.Maui.Media` fallback (cloud-TTS hook). Each surfaces word-boundary
  callbacks normalised into a `Word` event that drives the highlight.
- **`WebnovelScraper.cs`** — tuned for freewebnovel.com markup with OpenGraph
  fallbacks; turns a chapter **or** novel URL into a novel + episode list, and
  lazily fetches/caches chapter text.
- **`SyncService.cs`** — local-first, last-write-wins reconciliation with Firestore.

## Getting started
```bash
dotnet workload install maui
# add OpenSans-Regular.ttf / OpenSans-Semibold.ttf to Resources/Fonts (see its README)
# fill in your Firebase values — see FIREBASE_SETUP.md
dotnet build -t:Run -f net8.0-android      # or net8.0-ios / -maccatalyst / -windows10.0.19041.0
```

See **FIREBASE_SETUP.md** for backend config and security rules.
