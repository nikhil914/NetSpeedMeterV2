using System.Net.NetworkInformation;
using NetMonitorPro.Core.Interfaces;
using NetMonitorPro.Core.Models;

namespace NetMonitorPro.Core.Services;

/// <summary>
/// Monitors network adapter statistics using NetworkInterface API.
/// Polls at configurable intervals and fires StatsUpdated events.
/// </summary>
public sealed class NetworkMonitorService : INetworkMonitorService
{
    private readonly ISettingsService _settings;
    private Timer? _timer;
    private long _prevRecv;
    private long _prevSent;
    private string _selectedAdapter = string.Empty;
    private bool _disposed;
    private readonly object _lock = new();

    public event EventHandler<NetworkStats>? StatsUpdated;
    public NetworkStats? LatestStats { get; private set; }
    public bool IsMonitoring { get; private set; }

    public NetworkMonitorService(ISettingsService settings)
    {
        _settings = settings;
    }

    public void StartMonitoring()
    {
        if (IsMonitoring) return;

        // Resolve which adapter to monitor
        _selectedAdapter = _settings.Settings.SelectedAdapterName
                           ?? GetBestAdapter()?.Name
                           ?? string.Empty;

        // Seed counters
        var (recv, sent) = GetCumulativeBytes(_selectedAdapter);
        _prevRecv = recv;
        _prevSent = sent;

        IsMonitoring = true;
        var interval = Math.Clamp(_settings.Settings.UpdateIntervalMs, 250, 5000);
        _timer = new Timer(OnTick, null, TimeSpan.FromMilliseconds(interval), TimeSpan.FromMilliseconds(interval));
    }

    public void StopMonitoring()
    {
        IsMonitoring = false;
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
    }

    public List<AdapterInfo> GetAdapters()
    {
        var adapters = new List<AdapterInfo>();

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                var props = ni.GetIPProperties();
                var ipv4 = props.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                var ipv6 = props.UnicastAddresses
                    .FirstOrDefault(a => a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6);

                string mac = "";
                try { mac = FormatMac(ni.GetPhysicalAddress().GetAddressBytes()); }
                catch { /* some adapters don't support MAC */ }

                long speed = 0;
                try { speed = ni.Speed; }
                catch { /* some adapters don't report speed */ }

                var adapter = new AdapterInfo
                {
                    Name = ni.Name,
                    Description = ni.Description,
                    Type = ni.NetworkInterfaceType.ToString(),
                    Status = ni.OperationalStatus.ToString(),
                    MacAddress = mac,
                    IPv4Address = ipv4?.Address.ToString() ?? "N/A",
                    IPv4SubnetMask = ipv4?.IPv4Mask?.ToString() ?? "N/A",
                    IPv6Address = ipv6?.Address.ToString() ?? "N/A",
                    DefaultGateway = props.GatewayAddresses.FirstOrDefault()?.Address.ToString() ?? "N/A",
                    DnsServers = props.DnsAddresses.Select(d => d.ToString()).ToList(),
                    SpeedBps = speed
                };

                try
                {
                    var stats = ni.GetIPStatistics();
                    adapter.BytesSent = stats.BytesSent;
                    adapter.BytesReceived = stats.BytesReceived;
                }
                catch { /* Some virtual adapters don't support stats */ }

                adapters.Add(adapter);
            }
            catch
            {
                // Skip adapters that throw on any property access
            }
        }

        return adapters;
    }

    private void OnTick(object? state)
    {
        if (_disposed) return;

        lock (_lock)
        {
            try
            {
                var (recv, sent) = GetCumulativeBytes(_selectedAdapter);

                var downDelta = Math.Max(0, recv - _prevRecv);
                var upDelta = Math.Max(0, sent - _prevSent);

                // Convert delta to per-second (timer interval may vary)
                var intervalSec = _settings.Settings.UpdateIntervalMs / 1000.0;
                var downPerSec = (long)(downDelta / intervalSec);
                var upPerSec = (long)(upDelta / intervalSec);

                _prevRecv = recv;
                _prevSent = sent;

                var stats = new NetworkStats
                {
                    DownloadBytesPerSecond = downPerSec,
                    UploadBytesPerSecond = upPerSec,
                    TotalBytesReceived = recv,
                    TotalBytesSent = sent,
                    AdapterName = _selectedAdapter,
                    Timestamp = DateTime.Now
                };

                LatestStats = stats;
                StatsUpdated?.Invoke(this, stats);
            }
            catch
            {
                // Adapter may have been removed — skip this tick
            }
        }
    }

    /// <summary>
    /// Returns cumulative bytes (received, sent) for a named adapter, or all adapters combined.
    /// </summary>
    private static (long recv, long sent) GetCumulativeBytes(string adapterName)
    {
        long totalRecv = 0, totalSent = 0;

        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up) continue;
            if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

            if (!string.IsNullOrEmpty(adapterName) && ni.Name != adapterName)
                continue;

            try
            {
                var stats = ni.GetIPStatistics();
                totalRecv += stats.BytesReceived;
                totalSent += stats.BytesSent;
            }
            catch { /* skip unsupported adapters */ }
        }

        return (totalRecv, totalSent);
    }

    /// <summary>
    /// Heuristic: pick the first active non-loopback adapter that has a gateway (i.e. internet access).
    /// </summary>
    private static NetworkInterface? GetBestAdapter()
    {
        return NetworkInterface.GetAllNetworkInterfaces()
            .Where(ni => ni.OperationalStatus == OperationalStatus.Up
                         && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback
                         && ni.GetIPProperties().GatewayAddresses.Count > 0)
            .OrderByDescending(ni => ni.Speed)
            .FirstOrDefault();
    }

    private static string FormatMac(byte[] bytes)
    {
        return string.Join("-", bytes.Select(b => b.ToString("X2")));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
    }
}
