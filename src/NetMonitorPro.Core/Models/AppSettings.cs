using Newtonsoft.Json;

namespace NetMonitorPro.Core.Models;

/// <summary>
/// Persistent application settings, serialized to JSON.
/// </summary>
public class AppSettings
{
    // Overlay positioning
    public double OverlayLeft { get; set; } = -1;   // -1 = auto-position
    public double OverlayTop { get; set; } = -1;
    public bool OverlayAutoPosition { get; set; } = true;

    // Display
    public string FontFamily { get; set; } = "Segoe UI";
    public double FontSize { get; set; } = 13;
    public string DownloadColor { get; set; } = "#00E676";  // Green
    public string UploadColor { get; set; } = "#42A5F5";    // Blue
    public string BackgroundColor { get; set; } = "#1A1A2E"; // Dark navy
    public double BackgroundOpacity { get; set; } = 0.92;
    public double BorderRadius { get; set; } = 8;

    // Behavior
    public bool AlwaysOnTop { get; set; } = true;
    public bool ClickThroughEnabled { get; set; } = false;
    public int UpdateIntervalMs { get; set; } = 1000;
    public bool StartMinimized { get; set; } = false;
    public bool LaunchOnStartup { get; set; } = false;

    // Monitoring
    public string? SelectedAdapterName { get; set; }  // null = auto (first active)

    // Alerts
    public bool AlertOnSpeedDrop { get; set; } = true;
    public double SpeedDropThresholdMbps { get; set; } = 1.0;
    public bool AlertOnDataCap { get; set; } = false;
    public double DailyDataCapGB { get; set; } = 10.0;
    public bool AlertOnDisconnect { get; set; } = true;

    // Theme
    public bool DarkMode { get; set; } = true;
}
