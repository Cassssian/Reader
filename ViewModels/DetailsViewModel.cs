using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reader.Models;
using Reader.Services;

namespace Reader.ViewModels;

[QueryProperty(nameof(Id), "id")]
public partial class DetailsViewModel(Database db, FirebaseService fb, SyncService sync) : BaseViewModel
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

    // Pick an image and push it to Firebase Storage as the custom cover.
    [RelayCommand]
    async Task PickCover()
    {
        if (Novel is null) return;
        var file = await MediaPicker.Default.PickPhotoAsync();
        if (file is null) return;
        using var s = await file.OpenReadAsync();
        var url = await fb.UploadCover(Novel.Id, s);
        if (string.IsNullOrEmpty(url)) return;
        Novel.CustomCover = url;
        await sync.Push(Novel);
        OnPropertyChanged(nameof(Novel));
    }
}
