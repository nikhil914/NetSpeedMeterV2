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
        return FormatSpeed(bytesPerSecond, useBits: false);
    }

    public static string FormatSpeed(long bytesPerSecond, bool useBits)
    {
        if (useBits)
        {
            double bitsPerSecond = bytesPerSecond * 8.0;
            const double Kbps = 1000.0;
            const double Mbps = Kbps * 1000;
            const double Gbps = Mbps * 1000;

            return bitsPerSecond switch
            {
                >= Gbps => $"{bitsPerSecond / Gbps:F1} Gbps",
                >= Mbps => $"{bitsPerSecond / Mbps:F1} Mbps",
                >= Kbps => $"{bitsPerSecond / Kbps:F1} Kbps",
                _ => $"{bitsPerSecond:F0} bps"
            };
        }

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
        return FormatSpeedParts(bytesPerSecond, useBits: false);
    }

    public static (double Value, string Unit) FormatSpeedParts(long bytesPerSecond, bool useBits)
    {
        if (useBits)
        {
            double bitsPerSecond = bytesPerSecond * 8.0;
            const double Kbps = 1000.0;
            const double Mbps = Kbps * 1000;
            const double Gbps = Mbps * 1000;

            return bitsPerSecond switch
            {
                >= Gbps => (bitsPerSecond / Gbps, "Gbps"),
                >= Mbps => (bitsPerSecond / Mbps, "Mbps"),
                >= Kbps => (bitsPerSecond / Kbps, "Kbps"),
                _ => (bitsPerSecond, "bps")
            };
        }

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
