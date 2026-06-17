using Reader.ViewModels;

namespace Reader.Views;

public partial class ReaderPage : ContentPage
{
    readonly ReaderViewModel _vm;
    readonly Color _active = (Color)Application.Current!.Resources["Primary"];
    readonly Color _muted = (Color)Application.Current!.Resources["MutedColor"];

    public ReaderPage(ReaderViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;

        Lang.ItemsSource = vm.Languages.Select(l => l.Name).ToList();
        Lang.SelectedIndex = Array.FindIndex(vm.Languages, l => l.Code == vm.Lang);

        SpeedSlider.Value = vm.Speed;
        SpeedLbl.Text = $"{vm.Speed:0.0}x";

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

    double _lastY = -1;

    // Garde le mot prononcé à peu près centré ; throttle pour éviter les à-coups.
    void AutoScroll()
    {
        if (!_vm.ShowText || Body.Height <= 0) return;
        var y = _vm.Position * Math.Max(0, Body.Height - Scroll.Height);
        if (Math.Abs(y - _lastY) < 24) return;
        _lastY = y;
        Scroll.ScrollToAsync(0, y, false);
    }

    void UpdatePlay() => Play.Text = _vm.Playing ? "❚❚" : "▶";

    // Met en évidence le mode actif (fond plein) vs inactifs (transparent).
    void SyncModeButtons()
    {
        Button[] b = { M0, M1, M2 };
        for (int i = 0; i < b.Length; i++)
        {
            bool on = _vm.Mode == i;
            b[i].BackgroundColor = on ? _active : Colors.Transparent;
            b[i].TextColor = on ? Colors.White : _muted;
        }
    }

    void Mode(object? s, EventArgs e) => _vm.Mode = s == M0 ? 0 : s == M1 ? 1 : 2;

    void LangPick(object? s, EventArgs e)
    {
        if (Lang.SelectedIndex >= 0)
            _vm.ChangeLangCommand.Execute(_vm.Languages[Lang.SelectedIndex].Code);
    }

    void Seeked(object? s, EventArgs e) => _vm.SeekTo(Seek.Value);

    // Étiquette en direct, mais on n'applique (et relance la lecture) qu'au relâchement.
    void SpeedChanged(object? s, ValueChangedEventArgs e) => SpeedLbl.Text = $"{e.NewValue:0.0}x";
    void SpeedSet(object? s, EventArgs e) => _vm.Speed = Math.Round(SpeedSlider.Value, 1);

    async void Back(object? s, EventArgs e) => await Shell.Current.GoToAsync("..");

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Leave();
    }
}
