using Reader.Models;
using SQLite;

namespace Reader.Services;

// Local-first store. Everything reads/writes here; SyncService reconciles with Firestore.
public class Database
{
    SQLiteAsyncConnection? _db;

    async Task<SQLiteAsyncConnection> Conn()
    {
        if (_db is not null) return _db;
        var path = Path.Combine(FileSystem.AppDataDirectory, "reader.db3");
        _db = new SQLiteAsyncConnection(path,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
        await _db.CreateTableAsync<Webnovel>();
        await _db.CreateTableAsync<Episode>();
        return _db;
    }

    // --- Novels ---
    public async Task<List<Webnovel>> Novels() =>
        await (await Conn()).Table<Webnovel>().OrderByDescending(n => n.Updated).ToListAsync();

    public async Task<Webnovel?> Novel(string id) =>
        await (await Conn()).FindAsync<Webnovel>(id);

    public async Task Save(Webnovel n)
    {
        n.Updated = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        await (await Conn()).InsertOrReplaceAsync(n);
    }

    public async Task Delete(Webnovel n)
    {
        var c = await Conn();
        await c.DeleteAsync(n);
        await c.ExecuteAsync("DELETE FROM Episode WHERE NovelId = ?", n.Id);   // cascade
    }

    // --- Episodes ---
    public async Task<List<Episode>> Episodes(string novelId) =>
        await (await Conn()).Table<Episode>().Where(e => e.NovelId == novelId)
            .OrderBy(e => e.Index).ToListAsync();

    public async Task<Episode?> EpisodeAt(string novelId, int index) =>
        await (await Conn()).Table<Episode>()
            .Where(e => e.NovelId == novelId && e.Index == index).FirstOrDefaultAsync();

    public async Task SaveEpisodes(IEnumerable<Episode> eps) =>
        await (await Conn()).InsertAllAsync(eps, "OR IGNORE");

    public async Task Save(Episode e) => await (await Conn()).InsertOrReplaceAsync(e);
}
