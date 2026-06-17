using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reader.Models;
using Reader.Services;

namespace Reader.ViewModels;

[QueryProperty(nameof(Id), "id")]
public partial class DetailsViewModel(Database db, SyncService sync, SupabaseService supa) : BaseViewModel
{
    [ObservableProperty] string id = "";
    [ObservableProperty] Webnovel? novel;
    [ObservableProperty] bool canContinue;
    [ObservableProperty] string continueLabel = "";
    // Assignée en bloc (1 seule notification) → pas de gel sur les longues listes.
    [ObservableProperty] ObservableCollection<Episode> episodes = new();

    partial void OnIdChanged(string value) => _ = Load();

    async Task Load()
    {
        Busy = true;
        Novel = await db.Novel(Id);
        var eps = await db.Episodes(Id);
        Episodes = new ObservableCollection<Episode>(eps);

        // Bouton "Continuer" si une lecture a déjà commencé.
        CanContinue = Novel is not null && (Novel.Progress > 0 || Novel.LastEp > 0);
        if (CanContinue && Novel is not null)
            ContinueLabel = $"▶  Continuer · chapitre {Novel.LastEp + 1}";
        Busy = false;
    }

    [RelayCommand]
    async Task Open(Episode e) =>
        await Shell.Current.GoToAsync($"reader?id={Novel!.Id}&ep={e.Index}");

    [RelayCommand]
    async Task Continue()
    {
        if (Novel is null) return;
        await Shell.Current.GoToAsync($"reader?id={Novel.Id}&ep={Novel.LastEp}");
    }

    // Couverture perso : copie locale + upload Supabase si connecté.
    [RelayCommand]
    async Task PickCover()
    {
        if (Novel is null) return;
        var file = await MediaPicker.Default.PickPhotoAsync();
        if (file is null) return;

        var dir = Path.Combine(FileSystem.AppDataDirectory, "covers");
        Directory.CreateDirectory(dir);
        var dest = Path.Combine(dir, $"{Novel.Id}.jpg");
        using (var src = await file.OpenReadAsync())
        using (var dst = File.OpenWrite(dest))
            await src.CopyToAsync(dst);
        Novel.CustomCover = dest;

        if (supa.SignedIn)
        {
            using var stream = await file.OpenReadAsync();
            var remote = await supa.UploadCover(Novel.Id, stream);
            if (!string.IsNullOrEmpty(remote)) Novel.CustomCover = remote;
        }

        await sync.Push(Novel);
        OnPropertyChanged(nameof(Novel));
    }
}
