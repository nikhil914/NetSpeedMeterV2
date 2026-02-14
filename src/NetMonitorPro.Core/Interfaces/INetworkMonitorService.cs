using NetMonitorPro.Core.Models;

namespace NetMonitorPro.Core.Interfaces;

/// <summary>
/// Contract for real-time network speed monitoring.
/// </summary>
public interface INetworkMonitorService : IDisposable
{
    /// <summary>Fired when new speed statistics are available.</summary>
    event EventHandler<NetworkStats>? StatsUpdated;

    /// <summary>Start polling network adapter counters.</summary>
    void StartMonitoring();

    /// <summary>Stop monitoring.</summary>
    void StopMonitoring();

    /// <summary>Get supported network adapters.</summary>
    List<AdapterInfo> GetAdapters();

    /// <summary>Get the latest snapshot.</summary>
    NetworkStats? LatestStats { get; }

    /// <summary>Whether monitoring is active.</summary>
    bool IsMonitoring { get; }
}
