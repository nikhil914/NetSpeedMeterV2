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
        await _db.CreateTableAsync<HourlyUsage>();
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

    // ──────────── HourlyUsage ────────────

    public async Task UpsertHourlyUsageAsync(string date, int hour, string adapter,
        long bytesDown, long bytesUp, long peakDown, long peakUp)
    {
        var existing = await _db.Table<HourlyUsage>()
            .Where(h => h.Date == date && h.Hour == hour && h.AdapterName == adapter)
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
            await _db.InsertAsync(new HourlyUsage
            {
                Date = date,
                Hour = hour,
                AdapterName = adapter,
                BytesDownloaded = bytesDown,
                BytesUploaded = bytesUp,
                PeakDownloadSpeed = peakDown,
                PeakUploadSpeed = peakUp
            });
        }
    }

    public async Task<List<HourlyUsage>> GetHourlyUsageAsync(string date)
    {
        return await _db.Table<HourlyUsage>()
            .Where(h => h.Date == date)
            .OrderBy(h => h.Hour)
            .ToListAsync();
    }

    // ──────────── Aggregation Helpers ────────────

    /// <summary>
    /// Get total usage for a specific date (sum of all adapters).
    /// </summary>
    public async Task<(long BytesDown, long BytesUp, long PeakDown, long PeakUp)> GetDaySummaryAsync(string date)
    {
        var rows = await _db.Table<DailyUsage>()
            .Where(d => d.Date == date)
            .ToListAsync();

        return (
            rows.Sum(r => r.BytesDownloaded),
            rows.Sum(r => r.BytesUploaded),
            rows.Count > 0 ? rows.Max(r => r.PeakDownloadSpeed) : 0,
            rows.Count > 0 ? rows.Max(r => r.PeakUploadSpeed) : 0
        );
    }

    /// <summary>
    /// Get total usage for a month (aggregated from daily).
    /// </summary>
    public async Task<(long BytesDown, long BytesUp, long PeakDown, long PeakUp)> GetMonthSummaryAsync(int year, int month)
    {
        var prefix = $"{year:D4}-{month:D2}";
        var rows = await _db.Table<DailyUsage>()
            .Where(d => d.Date.StartsWith(prefix))
            .ToListAsync();

        return (
            rows.Sum(r => r.BytesDownloaded),
            rows.Sum(r => r.BytesUploaded),
            rows.Count > 0 ? rows.Max(r => r.PeakDownloadSpeed) : 0,
            rows.Count > 0 ? rows.Max(r => r.PeakUploadSpeed) : 0
        );
    }

    /// <summary>
    /// Get weekly usage (last 7 days from the given date, inclusive).
    /// </summary>
    public async Task<List<DailyUsage>> GetWeeklyUsageAsync(DateTime endDate)
    {
        var from = endDate.AddDays(-6).ToString("yyyy-MM-dd");
        var to = endDate.ToString("yyyy-MM-dd");
        return await _db.Table<DailyUsage>()
            .Where(d => d.Date.CompareTo(from) >= 0 && d.Date.CompareTo(to) <= 0)
            .OrderBy(d => d.Date)
            .ToListAsync();
    }

    // ──────────── ProcessHistory ────────────

    public async Task UpsertProcessUsageAsync(string date, string processName, string? exePath,
        long bytesDown, long bytesUp)
    {
        var existing = await _db.Table<ProcessHistory>()
            .Where(p => p.Date == date && p.ProcessName == processName)
            .FirstOrDefaultAsync();

        if (existing != null)
        {
            existing.TotalBytesDownloaded += bytesDown;
            existing.TotalBytesUploaded += bytesUp;
            if (exePath != null) existing.ExecutablePath = exePath;
            await _db.UpdateAsync(existing);
        }
        else
        {
            await _db.InsertAsync(new ProcessHistory
            {
                Date = date,
                ProcessName = processName,
                ExecutablePath = exePath,
                TotalBytesDownloaded = bytesDown,
                TotalBytesUploaded = bytesUp
            });
        }
    }

    public async Task<List<ProcessHistory>> GetProcessUsageAsync(string date)
    {
        return await _db.Table<ProcessHistory>()
            .Where(p => p.Date == date)
            .OrderByDescending(p => p.TotalBytesDownloaded + p.TotalBytesUploaded)
            .ToListAsync();
    }

    public async Task<List<ProcessHistory>> GetProcessUsageRangeAsync(string fromDate, string toDate)
    {
        // Get all process history within the date range, then aggregate in-memory
        var rows = await _db.Table<ProcessHistory>()
            .Where(p => p.Date.CompareTo(fromDate) >= 0 && p.Date.CompareTo(toDate) <= 0)
            .ToListAsync();

        // Aggregate by process name
        return rows
            .GroupBy(p => p.ProcessName)
            .Select(g => new ProcessHistory
            {
                ProcessName = g.Key,
                ExecutablePath = g.First().ExecutablePath,
                Date = fromDate,
                TotalBytesDownloaded = g.Sum(p => p.TotalBytesDownloaded),
                TotalBytesUploaded = g.Sum(p => p.TotalBytesUploaded)
            })
            .OrderByDescending(p => p.TotalBytesDownloaded + p.TotalBytesUploaded)
            .ToList();
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
        try
        {
            var session = await _db.GetAsync<SessionLog>(sessionId);
            session.SessionEnd = DateTime.Now.ToString("o");
            session.BytesDownloaded = bytesDown;
            session.BytesUploaded = bytesUp;
            session.AverageDownloadSpeed = avgDown;
            session.AverageUploadSpeed = avgUp;
            await _db.UpdateAsync(session);
        }
        catch { /* Session may not exist if DB init failed */ }
    }

    public async Task<List<SessionLog>> GetRecentSessionsAsync(int count = 20)
    {
        return await _db.Table<SessionLog>()
            .OrderByDescending(s => s.SessionStart)
            .Take(count)
            .ToListAsync();
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
