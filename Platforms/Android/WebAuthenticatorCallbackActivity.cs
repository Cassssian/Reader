using Android.App;
using Android.Content;
using Android.Content.PM;

namespace Reader;

// Receives the OAuth redirect (readerapp://auth) and hands it to Essentials WebAuthenticator.
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "readerapp", DataHost = "auth")]
public class WebAuthCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
