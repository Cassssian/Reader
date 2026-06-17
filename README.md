# Reader

Lecteur de webnovels cross-platform .NET MAUI (Android · iOS · macOS · Windows).
**Zéro service payant.**

## Stack technique (100 % gratuit)

| Rôle | Solution | Coût |
|---|---|---|
| Stockage local | SQLite (sqlite-net-pcl) | 0 € |
| Sync cloud optionnelle | **Supabase free tier** — sans CB | 0 € |
| Traduction | **MyMemory API** — sans clé | 0 € |
| TTS | APIs natives (Android.Speech.Tts / AVFoundation / System.Speech) | 0 € |
| Scraping | HtmlAgilityPack + HttpClient | 0 € |

## Fonctionnalités

- **Bibliothèque** — grille adaptative, couvertures auto, progression, état vide animé.
- **Ajout de roman** — URL directe (roman ou chapitre) ou navigation in-app + "Utiliser cette page".
- **Lecteur** — 3 modes (Audio / Audio+Texte / Texte), mot courant surligné, visualiseur de waveform,
  seekbar, skip ±10 s, vitesse 0.5×–2×, dropdown de langue avec traduction automatique.
- **Sync** — progression et thème synchronisés via Supabase (connexion Google, optionnelle).
- **Hors ligne** — le contenu des chapitres lus est mis en cache dans SQLite.

## Architecture

```
Models/        Webnovel · Episode · UserSettings
Services/      Database · SupabaseService · TtsService · TranslationService · WebnovelScraper · SyncService
ViewModels/    MainMenu · Details · Reader · AddNovel   (CommunityToolkit.Mvvm)
Views/         Splash · MainMenu · Details · Reader · AddNovel
Controls/      EpisodeCard · AudioWaveView · Converters
Platforms/     Android · iOS · MacCatalyst · Windows
```

## Démarrage rapide

```bash
dotnet workload install maui
# Ajouter OpenSans-Regular.ttf / OpenSans-Semibold.ttf dans Resources/Fonts/
# Renseigner URL et AnonKey Supabase dans Services/SupabaseService.cs
dotnet build -t:Run -f net8.0-android
```

Voir **SUPABASE_SETUP.md** pour les tables SQL, les règles RLS et le OAuth Google.
