using Reader.ViewModels;

namespace Reader.Views;

public partial class ReaderPage : ContentPage
{
    readonly ReaderViewModel _vm;

    public ReaderPage(ReaderViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        // Populate language dropdown (display names) and preselect the synced choice.
        Lang.ItemsSource = vm.Languages.Select(l => l.Name).ToList();
        Lang.SelectedIndex = Array.FindIndex(vm.Languages, l => l.Code == vm.Lang);

        _vm.PropertyChanged += OnVmChanged;
        SyncModeButtons();
        UpdatePlay();
    }

    void OnVmChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(_vm.Playing)) UpdatePlay();
        else if (e.PropertyName == nameof(_vm.Mode)) SyncModeButtons();
        else if (e.PropertyName == nameof(_vm.Position)) AutoScroll();
    }

    // Keep the live word roughly centered as speech advances.
    void AutoScroll()
    {
        if (!_vm.ShowText || Body.Height <= 0) return;
        var y = _vm.Position * Math.Max(0, Body.Height - Scroll.Height);
        Scroll.ScrollToAsync(0, y, false);
    }

    void UpdatePlay() => Play.Text = _vm.Playing ? "❚❚" : "▶";

    void SyncModeButtons()
    {
        // Highlight the active mode tab.
        M0.Opacity = _vm.Mode == 0 ? 1 : 0.4;
        M1.Opacity = _vm.Mode == 1 ? 1 : 0.4;
        M2.Opacity = _vm.Mode == 2 ? 1 : 0.4;
    }

    void Mode(object? s, EventArgs e) => _vm.Mode = s == M0 ? 0 : s == M1 ? 1 : 2;

    void LangPick(object? s, EventArgs e)
    {
        if (Lang.SelectedIndex >= 0)
            _vm.ChangeLangCommand.Execute(_vm.Languages[Lang.SelectedIndex].Code);
    }

    void Seeked(object? s, EventArgs e) => _vm.SeekTo(Seek.Value);

    async void Back(object? s, EventArgs e) => await Shell.Current.GoToAsync("..");

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Leave();                 // stop TTS + persist progress when navigating away
    }
}
