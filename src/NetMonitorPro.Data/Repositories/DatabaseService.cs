using SQLite;
using NetMonitorPro.Data.Entities;

namespace NetMonitorPro.Data.Repositories;

/// <summary>
/// SQLite database manager. Handles schema creation, CRUD, and aggregation.
/// </summary>
public sealed class DatabaseService : IDisposable
{
    private readonly SQLiteAsyncConnection _db;
    private bool _initialized;

    public DatabaseService()
    {
        var dbDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetMonitorPro");
        Directory.CreateDirectory(dbDir);

        var dbPath = Path.Combine(dbDir, "netmonitor.db");
        _db = new SQLiteAsyncConnection(dbPath);
    }

    public async Task InitializeAsync()
    {
        if (_initialized) return;

        await _db.CreateTableAsync<DailyUsage>();
        await _db.CreateTableAsync<SessionLog>();
        await _db.CreateTableAsync<ProcessHistory>();
        await _db.CreateTableAsync<NetworkEvent>();
        await _db.CreateTableAsync<SpeedTestRecord>();

        _initialized = true;
    }

    // ──────────── DailyUsage ────────────

    public async Task UpsertDailyUsageAsync(string date, string adapter,
        long bytesDown, long bytesUp, long peakDown, long peakUp)
    {
        var existing = await _db.Table<DailyUsage>()
            .Where(d => d.Date == date && d.AdapterName == adapter)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.BytesDownloaded += bytesDown;
            existing.BytesUploaded += bytesUp;
            existing.PeakDownloadSpeed = Math.Max(existing.PeakDownloadSpeed, peakDown);
            existing.PeakUploadSpeed = Math.Max(existing.PeakUploadSpeed, peakUp);
            await _db.UpdateAsync(existing);
        }
        else
        {
            await _db.InsertAsync(new DailyUsage
            {
                Date = date,
                AdapterName = adapter,
                BytesDownloaded = bytesDown,
                BytesUploaded = bytesUp,
                PeakDownloadSpeed = peakDown,
                PeakUploadSpeed = peakUp
            });
        }
    }

    public async Task<List<DailyUsage>> GetDailyUsageAsync(DateTime from, DateTime to)
    {
        var fromStr = from.ToString("yyyy-MM-dd");
        var toStr = to.ToString("yyyy-MM-dd");
        return await _db.Table<DailyUsage>()
            .Where(d => d.Date.CompareTo(fromStr) >= 0 && d.Date.CompareTo(toStr) <= 0)
            .OrderBy(d => d.Date)
            .ToListAsync();
    }

    // ──────────── SessionLog ────────────

    public async Task<int> StartSessionAsync(string adapter)
    {
        var session = new SessionLog
        {
            SessionStart = DateTime.Now.ToString("o"),
            AdapterName = adapter
        };
        await _db.InsertAsync(session);
        return session.Id;
    }

    public async Task EndSessionAsync(int sessionId, long bytesDown, long bytesUp, long avgDown, long avgUp)
    {
        var session = await _db.GetAsync<SessionLog>(sessionId);
        session.SessionEnd = DateTime.Now.ToString("o");
        session.BytesDownloaded = bytesDown;
        session.BytesUploaded = bytesUp;
        session.AverageDownloadSpeed = avgDown;
        session.AverageUploadSpeed = avgUp;
        await _db.UpdateAsync(session);
    }

    // ──────────── NetworkEvents ────────────

    public async Task LogEventAsync(string eventType, string description, string severity = "INFO")
    {
        await _db.InsertAsync(new NetworkEvent
        {
            Timestamp = DateTime.Now.ToString("o"),
            EventType = eventType,
            Description = description,
            Severity = severity
        });
    }

    public async Task<List<NetworkEvent>> GetRecentEventsAsync(int count = 50)
    {
        return await _db.Table<NetworkEvent>()
            .OrderByDescending(e => e.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    // ──────────── SpeedTest ────────────

    public async Task SaveSpeedTestAsync(SpeedTestRecord record)
    {
        record.Timestamp = DateTime.Now.ToString("o");
        await _db.InsertAsync(record);
    }

    public async Task<List<SpeedTestRecord>> GetSpeedTestHistoryAsync(int count = 20)
    {
        return await _db.Table<SpeedTestRecord>()
            .OrderByDescending(r => r.Timestamp)
            .Take(count)
            .ToListAsync();
    }

    // ──────────── Export ────────────

    public async Task<List<DailyUsage>> GetAllDailyUsageAsync()
    {
        return await _db.Table<DailyUsage>().OrderBy(d => d.Date).ToListAsync();
    }

    public void Dispose()
    {
        _db.CloseAsync().GetAwaiter().GetResult();
    }
}
