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
    public ObservableCollection<Episode> Episodes { get; } = new();

    partial void OnIdChanged(string value) => _ = Load();

    async Task Load()
    {
        Busy = true;
        Novel = await db.Novel(Id);
        Episodes.Clear();
        foreach (var e in await db.Episodes(Id)) Episodes.Add(e);
        Busy = false;
    }

    [RelayCommand]
    async Task Open(Episode e) =>
        await Shell.Current.GoToAsync($"reader?id={Novel!.Id}&ep={e.Index}");

    // Couverture personnalisée : sauvegardée localement et, si connecté,
    // uploadée vers Supabase Storage (bucket "covers", public, gratuit).
    [RelayCommand]
    async Task PickCover()
    {
        if (Novel is null) return;
        var file = await MediaPicker.Default.PickPhotoAsync();
        if (file is null) return;

        // Copie locale (utilisée même hors ligne).
        var localPath = Path.Combine(FileSystem.AppDataDirectory, "covers");
        Directory.CreateDirectory(localPath);
        var dest = Path.Combine(localPath, $"{Novel.Id}.jpg");
        using (var src = await file.OpenReadAsync())
        using (var dst = File.OpenWrite(dest))
            await src.CopyToAsync(dst);

        Novel.CustomCover = dest;   // chemin local par défaut

        // Si connecté → upload Supabase Storage et on stocke l'URL publique.
        if (supa.SignedIn)
        {
            using var stream = await file.OpenReadAsync();
            var remoteUrl = await supa.UploadCover(Novel.Id, stream);
            if (!string.IsNullOrEmpty(remoteUrl)) Novel.CustomCover = remoteUrl;
        }

        await sync.Push(Novel);
        OnPropertyChanged(nameof(Novel));
    }
}
