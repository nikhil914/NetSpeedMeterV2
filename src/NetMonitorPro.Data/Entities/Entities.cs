using SQLite;

namespace NetMonitorPro.Data.Entities;

[Table("DailyUsage")]
public class DailyUsage
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "IX_DailyUsage_DateAdapter", Order = 1, Unique = true)]
    public string Date { get; set; } = string.Empty; // YYYY-MM-DD

    [Indexed(Name = "IX_DailyUsage_DateAdapter", Order = 2, Unique = true)]
    public string AdapterName { get; set; } = string.Empty;

    public long BytesDownloaded { get; set; }
    public long BytesUploaded { get; set; }
    public long PeakDownloadSpeed { get; set; }
    public long PeakUploadSpeed { get; set; }
    public int TotalConnections { get; set; }
}

[Table("HourlyUsage")]
public class HourlyUsage
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "IX_HourlyUsage_DateHourAdapter", Order = 1, Unique = true)]
    public string Date { get; set; } = string.Empty; // YYYY-MM-DD

    [Indexed(Name = "IX_HourlyUsage_DateHourAdapter", Order = 2, Unique = true)]
    public int Hour { get; set; } // 0-23

    [Indexed(Name = "IX_HourlyUsage_DateHourAdapter", Order = 3, Unique = true)]
    public string AdapterName { get; set; } = string.Empty;

    public long BytesDownloaded { get; set; }
    public long BytesUploaded { get; set; }
    public long PeakDownloadSpeed { get; set; }
    public long PeakUploadSpeed { get; set; }
}

[Table("SessionLog")]
public class SessionLog
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string SessionStart { get; set; } = string.Empty;
    public string? SessionEnd { get; set; }
    public string AdapterName { get; set; } = string.Empty;
    public long BytesDownloaded { get; set; }
    public long BytesUploaded { get; set; }
    public long AverageDownloadSpeed { get; set; }
    public long AverageUploadSpeed { get; set; }
}

[Table("ProcessHistory")]
public class ProcessHistory
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed(Name = "IX_ProcessHistory_DateName", Order = 1, Unique = true)]
    public string Date { get; set; } = string.Empty;

    [Indexed(Name = "IX_ProcessHistory_DateName", Order = 2, Unique = true)]
    public string ProcessName { get; set; } = string.Empty;

    public string? ExecutablePath { get; set; }
    public long TotalBytesDownloaded { get; set; }
    public long TotalBytesUploaded { get; set; }
}

[Table("NetworkEvents")]
public class NetworkEvent
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Timestamp { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty; // SPEED_DROP, DISCONNECT, THRESHOLD_EXCEEDED
    public string? Description { get; set; }
    public string Severity { get; set; } = "INFO"; // INFO, WARNING, CRITICAL
}

[Table("SpeedTestHistory")]
public class SpeedTestRecord
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    public string Timestamp { get; set; } = string.Empty;
    public double DownloadSpeedMbps { get; set; }
    public double UploadSpeedMbps { get; set; }
    public int LatencyMs { get; set; }
    public double? Jitter { get; set; }
    public string? TestServer { get; set; }
    public double? PacketLoss { get; set; }
}
