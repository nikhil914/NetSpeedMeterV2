namespace NetMonitorPro.Core.Models;

/// <summary>
/// Defines all color values for a single application theme.
/// </summary>
public class ThemeDefinition
{
    public string Name { get; set; } = "Dark";
    public string DisplayName { get; set; } = "Dark (Default)";

    // Backgrounds
    public string WindowBg { get; set; } = "#0D1117";
    public string SidebarBg { get; set; } = "#0D1117";
    public string SidebarBorder { get; set; } = "#21262D";
    public string CardBg { get; set; } = "#161B22";
    public string MiniCardBg { get; set; } = "#1C2333";

    // Text
    public string TextPrimary { get; set; } = "#E6EDF3";
    public string TextSecondary { get; set; } = "#8B949E";
    public string TextMuted { get; set; } = "#484F58";

    // Accent colors
    public string AccentGreen { get; set; } = "#00E676";
    public string AccentBlue { get; set; } = "#42A5F5";
    public string AccentPurple { get; set; } = "#BB86FC";
    public string AccentOrange { get; set; } = "#FF7043";
    public string AccentYellow { get; set; } = "#FFD54F";
    public string AccentPink { get; set; } = "#CE93D8";

    // UI elements
    public string ButtonBg { get; set; } = "#21262D";
    public string ButtonHoverBg { get; set; } = "#30363D";
    public string ActionBg { get; set; } = "#238636";
    public string ActionHoverBg { get; set; } = "#2EA043";
    public string SeparatorColor { get; set; } = "#21262D";
    public string SidebarHoverBg { get; set; } = "#1C2333";
    public string InputBg { get; set; } = "#21262D";
    public string InputBorder { get; set; } = "#30363D";
    public string StatusBarBg { get; set; } = "#161B22";
    public string ChartGridColor { get; set; } = "#21262D";

    // Overlay (floating meter) colors
    public string OverlayGradientStart { get; set; } = "#1A1A2E";
    public string OverlayGradientMid { get; set; } = "#16213E";
    public string OverlayGradientEnd { get; set; } = "#0F3460";
    public string OverlaySeparator { get; set; } = "#334466";
    public string OverlayDownloadGlow { get; set; } = "#00E676";
    public string OverlayUploadGlow { get; set; } = "#42A5F5";
    public string OverlayDownloadUnitColor { get; set; } = "#80E6A0";
    public string OverlayUploadUnitColor { get; set; } = "#80B0E0";
}
