using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reader.Models;
using Reader.Services;

namespace Reader.ViewModels;

public partial class MainMenuViewModel(Database db, SyncService sync, SupabaseService supa) : BaseViewModel
{
    public ObservableCollection<Webnovel> Novels { get; } = new();

    [ObservableProperty] bool empty;
    [ObservableProperty] bool dark = sync.Settings.Dark;
    [ObservableProperty] bool signedIn = supa.SignedIn;

    public async Task Load()
    {
        Busy = true;
        Novels.Clear();
        foreach (var n in await db.Novels()) Novels.Add(n);
        Empty = Novels.Count == 0;
        Busy = false;
    }

    [RelayCommand]
    async Task Open(Webnovel n) => await Shell.Current.GoToAsync($"details?id={n.Id}");

    [RelayCommand]
    async Task Add() => await Shell.Current.GoToAsync("add");

    [RelayCommand]
    async Task Delete(Webnovel n)
    {
        await db.Delete(n);
        Novels.Remove(n);
        Empty = Novels.Count == 0;
    }

    [RelayCommand]
    async Task ToggleTheme()
    {
        Dark = !Dark;
        await sync.SetTheme(Dark);
    }

    // Connexion optionnelle pour activer la sync cross-device (Supabase free tier).
    [RelayCommand]
    async Task SignIn()
    {
        Busy = true;
        SignedIn = await sync.SignIn();
        if (SignedIn) await Load();
        Dark = sync.Settings.Dark;
        Busy = false;
    }
}
