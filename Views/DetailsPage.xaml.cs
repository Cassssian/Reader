using Reader.ViewModels;

namespace Reader.Views;

public partial class DetailsPage : ContentPage
{
    public DetailsPage(DetailsViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    async void Back(object? s, EventArgs e) => await Shell.Current.GoToAsync("..");

    // Episode tiles are wider than novel cards; ~220px each, clamped 2..5.
    protected override void OnSizeAllocated(double w, double h)
    {
        base.OnSizeAllocated(w, h);
        if (w <= 0 || Grid.ItemsLayout is not GridItemsLayout g) return;
        g.Span = Math.Clamp((int)(w / 220), 2, 5);
    }
}
