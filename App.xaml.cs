using Reader.Services;
using Reader.Views;

namespace Reader;

public partial class App : Application
{
    public App(SyncService sync)
    {
        InitializeComponent();
        // Apply persisted theme immediately so there's no flash of the wrong palette.
        UserAppTheme = Preferences.Get("dark", true) ? AppTheme.Dark : AppTheme.Light;
        MainPage = new SplashPage();   // animates, then swaps in the AppShell
        _ = sync.InitAsync();
    }
}
