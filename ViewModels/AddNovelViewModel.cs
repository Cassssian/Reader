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
        Busy = true; Status = "Fetching metadata…";
        try
        {
            var (novel, eps) = await scraper.Fetch(Url);
            await db.Save(novel);
            await db.SaveEpisodes(eps);
            await sync.Push(novel);
            Status = "";
            await Shell.Current.GoToAsync("..");   // back to main; it reloads on appearing
        }
        catch (Exception e)
        {
            Status = $"Could not read that URL. {e.Message}";
        }
        finally { Busy = false; }
    }

    [RelayCommand]
    async Task Cancel() => await Shell.Current.GoToAsync("..");
}
