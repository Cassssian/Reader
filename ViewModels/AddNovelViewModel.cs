using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Reader.Services;

namespace Reader.ViewModels;

public partial class AddNovelViewModel(Database db, SyncService sync, WebnovelScraper scraper) : BaseViewModel
{
    [ObservableProperty] string url = "";
    [ObservableProperty] string status = "";

    // freewebnovel search url; the in-app browser lands here and the user pastes/opens a result.
    public string SearchUrl => "https://freewebnovel.com/search";

    [RelayCommand]
    async Task Fetch()
    {
        if (string.IsNullOrWhiteSpace(Url)) return;
        Busy = true; Status = "Récupération des métadonnées…";
        try
        {
            var (novel, eps) = await scraper.Fetch(Url);
            await db.Save(novel);
            await db.SaveEpisodes(eps);
            await sync.Push(novel);
            // Si 0 chapitre, on prévient mais on ajoute quand même le roman.
            if (eps.Count == 0)
            {
                Status = "Roman ajouté, mais aucun chapitre trouvé (site non supporté).";
                return;
            }
            Status = $"{eps.Count} chapitres trouvés ✓";
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception e)
        {
            Status = $"Impossible de lire cette URL. {e.Message}";
        }
        finally { Busy = false; }
    }

    [RelayCommand]
    async Task Cancel() => await Shell.Current.GoToAsync("..");
}
