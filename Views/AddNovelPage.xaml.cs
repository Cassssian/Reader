using Reader.ViewModels;

namespace Reader.Views;

public partial class AddNovelPage : ContentPage
{
    readonly AddNovelViewModel _vm;
    string _current = "";

    public AddNovelPage(AddNovelViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        Web.Source = _vm.SearchUrl;     // land on the supported site's search page
    }

    void OnNav(object? s, WebNavigatedEventArgs e) => _current = e.Url;

    // Grab whatever the user browsed to and hand it to the scraper.
    void UsePage(object? s, EventArgs e) => _vm.Url = _current;
}
