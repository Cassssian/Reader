namespace Reader.Views;

public partial class SplashPage : ContentPage
{
    public SplashPage() => InitializeComponent();

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        // Mobile: quick minimalist fade + pop. Desktop: same intro plus a stepped progress bar.
        await Task.WhenAll(
            Stack.FadeTo(1, 400, Easing.CubicOut),
            Logo.ScaleTo(1, 500, Easing.SpringOut),
            Logo.RotateTo(360, 700, Easing.CubicInOut));

        if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
        {
            string[] steps = { "Loading library…", "Syncing settings…", "Preparing reader…", "Ready" };
            for (int i = 0; i < steps.Length; i++)
            {
                Step.Text = steps[i];
                await Bar.ProgressTo((i + 1) / (double)steps.Length, 250, Easing.CubicInOut);
            }
        }
        else
        {
            await Task.Delay(600);
        }

        Application.Current!.MainPage = new AppShell();
    }
}
