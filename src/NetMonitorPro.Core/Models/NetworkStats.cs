namespace NetMonitorPro.Core.Models;

/// <summary>
/// Real-time network speed statistics snapshot.
/// </summary>
public class NetworkStats
{
    public long DownloadBytesPerSecond { get; set; }
    public long UploadBytesPerSecond { get; set; }
    public long TotalBytesReceived { get; set; }
    public long TotalBytesSent { get; set; }
    public string AdapterName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    public string FormattedDownloadSpeed => FormatSpeed(DownloadBytesPerSecond);
    public string FormattedUploadSpeed => FormatSpeed(UploadBytesPerSecond);

    public static string FormatSpeed(long bytesPerSecond)
    {
        const double KB = 1024.0;
        const double MB = KB * 1024;
        const double GB = MB * 1024;

        return bytesPerSecond switch
        {
            >= (long)GB => $"{bytesPerSecond / GB:F1} GB/s",
            >= (long)MB => $"{bytesPerSecond / MB:F1} MB/s",
            >= (long)KB => $"{bytesPerSecond / KB:F1} KB/s",
            _ => $"{bytesPerSecond} B/s"
        };
    }

    public static (double Value, string Unit) FormatSpeedParts(long bytesPerSecond)
    {
        const double KB = 1024.0;
        const double MB = KB * 1024;
        const double GB = MB * 1024;

        return bytesPerSecond switch
        {
            >= (long)GB => (bytesPerSecond / GB, "GB/s"),
            >= (long)MB => (bytesPerSecond / MB, "MB/s"),
            >= (long)KB => (bytesPerSecond / KB, "KB/s"),
            _ => (bytesPerSecond, "B/s")
        };
    }
}
