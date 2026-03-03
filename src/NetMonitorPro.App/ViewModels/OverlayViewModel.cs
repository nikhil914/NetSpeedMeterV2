using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetMonitorPro.Core.Interfaces;
using NetMonitorPro.Core.Models;

namespace NetMonitorPro.App.ViewModels;

/// <summary>
/// ViewModel for the floating overlay widget.
/// Subscribes to network stats and formats them for display.
/// </summary>
public partial class OverlayViewModel : ObservableObject, IDisposable
{
    private readonly INetworkMonitorService _monitor;
    private readonly ISettingsService _settings;
    private DateTime _lastUiUpdate = DateTime.MinValue;
    private bool _disposed;

    [ObservableProperty]
    private string _downloadSpeed = "0 B/s";

    [ObservableProperty]
    private string _uploadSpeed = "0 B/s";

    [ObservableProperty]
    private string _downloadValue = "0";

    [ObservableProperty]
    private string _downloadUnit = "B/s";

    [ObservableProperty]
    private string _uploadValue = "0";

    [ObservableProperty]
    private string _uploadUnit = "B/s";

    [ObservableProperty]
    private bool _isAlwaysOnTop = true;

    [ObservableProperty]
    private double _backgroundOpacity = 0.92;

    public OverlayViewModel(INetworkMonitorService monitor, ISettingsService settings)
    {
        _monitor = monitor;
        _settings = settings;

        IsAlwaysOnTop = settings.Settings.AlwaysOnTop;
        BackgroundOpacity = settings.Settings.BackgroundOpacity;

        _monitor.StatsUpdated += OnStatsUpdated;

        if (!_monitor.IsMonitoring)
            _monitor.StartMonitoring();
    }

    private void OnStatsUpdated(object? sender, NetworkStats stats)
    {
        // Throttle UI updates to avoid excessive dispatching
        var now = DateTime.Now;
        if ((now - _lastUiUpdate).TotalMilliseconds < 200) return;
        _lastUiUpdate = now;

        var useBits = _settings.Settings.SpeedUnitBits;
        var (dlVal, dlUnit) = NetworkStats.FormatSpeedParts(stats.DownloadBytesPerSecond, useBits);
        var (ulVal, ulUnit) = NetworkStats.FormatSpeedParts(stats.UploadBytesPerSecond, useBits);

        DownloadValue = dlVal.ToString("F1");
        DownloadUnit = dlUnit;
        UploadValue = ulVal.ToString("F1");
        UploadUnit = ulUnit;

        DownloadSpeed = NetworkStats.FormatSpeed(stats.DownloadBytesPerSecond, useBits);
        UploadSpeed = NetworkStats.FormatSpeed(stats.UploadBytesPerSecond, useBits);
    }

    partial void OnIsAlwaysOnTopChanged(bool value)
    {
        _settings.Settings.AlwaysOnTop = value;
        _settings.Save();
    }

    [RelayCommand]
    private void QuitApplication()
    {
        _settings.Save();
        System.Windows.Application.Current.Shutdown();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _monitor.StatsUpdated -= OnStatsUpdated;
    }
}
