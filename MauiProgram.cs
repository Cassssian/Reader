using Microsoft.Extensions.Logging;
using Reader.Services;
using Reader.ViewModels;
using Reader.Views;

namespace Reader;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var b = MauiApp.CreateBuilder();
        b.UseMauiApp<App>()
            .ConfigureFonts(f =>
            {
                f.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                f.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // --- Services (100 % gratuits) ---
        b.Services.AddSingleton<Database>();
        b.Services.AddSingleton<SupabaseService>();          // sync cloud optionnelle — free tier
        b.Services.AddSingleton<ITtsService>(_ => TtsService.Create());  // APIs natives
        b.Services.AddSingleton<TranslationService>();       // MyMemory — sans clé, sans CB
        b.Services.AddSingleton<WebnovelScraper>();
        b.Services.AddSingleton<SyncService>();

        // --- ViewModels ---
        b.Services.AddSingleton<MainMenuViewModel>();
        b.Services.AddTransient<DetailsViewModel>();
        b.Services.AddTransient<ReaderViewModel>();
        b.Services.AddTransient<AddNovelViewModel>();

        // --- Pages ---
        b.Services.AddSingleton<MainMenuPage>();
        b.Services.AddTransient<DetailsPage>();
        b.Services.AddTransient<ReaderPage>();
        b.Services.AddTransient<AddNovelPage>();

#if DEBUG
        b.Logging.AddDebug();
#endif
        return b.Build();
    }
}
