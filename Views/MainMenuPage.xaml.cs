using Reader.ViewModels;

namespace Reader.Views;

public partial class MainMenuPage : ContentPage
{
    readonly MainMenuViewModel _vm;

    public MainMenuPage(MainMenuViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _vm.Load();
        Animate();
    }

    // Adaptive columns: ~170px per card, clamped 2..6.
    protected override void OnSizeAllocated(double w, double h)
    {
        base.OnSizeAllocated(w, h);
        if (w <= 0 || Grid.ItemsLayout is not GridItemsLayout g) return;
        g.Span = Math.Clamp((int)(w / 170), 2, 6);
    }

    async void Animate()
    {
        // Gentle pulsing search icon for the empty state.
        if (!_vm.Empty) return;
        while (_vm.Empty)
        {
            await EmptyIcon.ScaleTo(1.15, 700, Easing.SinInOut);
            await EmptyIcon.ScaleTo(1.0, 700, Easing.SinInOut);
        }
    }
}
