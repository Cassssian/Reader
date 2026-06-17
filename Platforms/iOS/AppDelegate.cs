using Foundation;

namespace Reader;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

    // Route the OAuth callback url back into Essentials WebAuthenticator.
    public override bool OpenUrl(UIKit.UIApplication app, NSUrl url, NSDictionary options) =>
        Microsoft.Maui.Authentication.WebAuthenticator.Default.OpenUrl(new Uri(url.AbsoluteString!))
        || base.OpenUrl(app, url, options);
}
