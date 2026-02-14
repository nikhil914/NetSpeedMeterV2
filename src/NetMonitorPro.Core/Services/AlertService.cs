using NetMonitorPro.Core.Interfaces;
using NetMonitorPro.Core.Models;

namespace NetMonitorPro.Core.Services;

/// <summary>
/// Monitors network conditions and fires alerts based on user thresholds:
/// - Speed drops below threshold
/// - Daily data cap exceeded
/// - Network disconnect detected
/// </summary>
public sealed class AlertService : IDisposable
{
    private readonly INetworkMonitorService _monitor;
    private readonly ISettingsService _settings;
    private bool _disposed;
    private bool _wasConnected = true;
    private DateTime _lastAlertTime = DateTime.MinValue;
    private long _todayBytesDown;
    private long _todayBytesUp;
    private DateTime _todayDate = DateTime.Today;

    /// Fires when an alert condition is detected: (alertType, message)
    public event Action<string, string>? AlertTriggered;

    public AlertService(INetworkMonitorService monitor, ISettingsService settings)
    {
        _monitor = monitor;
        _settings = settings;
        _monitor.StatsUpdated += OnStatsUpdated;
    }

    private void OnStatsUpdated(object? sender, NetworkStats stats)
    {
        if (_disposed) return;

        var s = _settings.Settings;

        // Reset daily counters at midnight
        if (DateTime.Today != _todayDate)
        {
            _todayDate = DateTime.Today;
            _todayBytesDown = 0;
            _todayBytesUp = 0;
        }

        // Accumulate daily usage
        _todayBytesDown += stats.DownloadBytesPerSecond;
        _todayBytesUp += stats.UploadBytesPerSecond;

        // Check disconnect
        bool isConnected = stats.DownloadBytesPerSecond > 0 || stats.UploadBytesPerSecond > 0;
        if (s.AlertOnDisconnect && _wasConnected && !isConnected)
        {
            FireAlert("Disconnect", "⚠ Network connection lost!");
        }
        _wasConnected = isConnected;

        // Check speed drop
        if (s.AlertOnSpeedDrop && isConnected)
        {
            double currentMbps = stats.DownloadBytesPerSecond * 8.0 / 1_000_000.0;
            if (currentMbps > 0 && currentMbps < s.SpeedDropThresholdMbps)
            {
                FireAlert("SpeedDrop", $"⚡ Speed dropped to {currentMbps:F1} Mbps (threshold: {s.SpeedDropThresholdMbps} Mbps)");
            }
        }

        // Check data cap
        if (s.AlertOnDataCap)
        {
            double totalGB = (_todayBytesDown + _todayBytesUp) / (1024.0 * 1024.0 * 1024.0);
            if (totalGB >= s.DailyDataCapGB)
            {
                FireAlert("DataCap", $"📊 Daily data cap reached: {totalGB:F2} GB / {s.DailyDataCapGB:F1} GB");
            }
        }
    }

    private void FireAlert(string type, string message)
    {
        // Rate-limit alerts: minimum 30 seconds between same type
        if ((DateTime.Now - _lastAlertTime).TotalSeconds < 30) return;
        _lastAlertTime = DateTime.Now;
        AlertTriggered?.Invoke(type, message);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _monitor.StatsUpdated -= OnStatsUpdated;
    }
}
