namespace NetMonitorPro.Core.Models;

/// <summary>
/// Information about a network adapter.
/// </summary>
public class AdapterInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;      // Ethernet, Wireless80211, etc.
    public string Status { get; set; } = string.Empty;     // Up, Down
    public string MacAddress { get; set; } = string.Empty;
    public string? IPv4Address { get; set; }
    public string? IPv4SubnetMask { get; set; }
    public string? IPv6Address { get; set; }
    public string? DefaultGateway { get; set; }
    public List<string> DnsServers { get; set; } = new();
    public long SpeedBps { get; set; }                     // Link speed in bits/sec
    public long BytesSent { get; set; }
    public long BytesReceived { get; set; }

    public string FormattedSpeed => SpeedBps switch
    {
        >= 1_000_000_000 => $"{SpeedBps / 1_000_000_000.0:F0} Gbps",
        >= 1_000_000 => $"{SpeedBps / 1_000_000.0:F0} Mbps",
        >= 1_000 => $"{SpeedBps / 1_000.0:F0} Kbps",
        _ => $"{SpeedBps} bps"
    };
}
