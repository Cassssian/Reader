using Reader.Views;

namespace Reader;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        // Detail routes are registered (not in the visual tree) for GoToAsync navigation.
        Routing.RegisterRoute("details", typeof(DetailsPage));
        Routing.RegisterRoute("reader", typeof(ReaderPage));
        Routing.RegisterRoute("add", typeof(AddNovelPage));
    }
}
