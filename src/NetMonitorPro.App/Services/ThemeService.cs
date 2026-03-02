using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using NetMonitorPro.Core.Models;

namespace NetMonitorPro.App.Services;

/// <summary>
/// Manages application theme switching by updating application-level resources.
/// </summary>
public sealed class ThemeService
{
    private static readonly Dictionary<string, ThemeDefinition> _themes = new()
    {
        ["Default"] = new ThemeDefinition
        {
            Name = "Default",
            DisplayName = "Default",
            WindowBg = "#0D1117",
            SidebarBg = "#0D1117",
            SidebarBorder = "#21262D",
            CardBg = "#161B22",
            MiniCardBg = "#1C2333",
            TextPrimary = "#E6EDF3",
            TextSecondary = "#8B949E",
            TextMuted = "#484F58",
            AccentGreen = "#00E676",
            AccentBlue = "#42A5F5",
            AccentPurple = "#BB86FC",
            AccentOrange = "#FF7043",
            AccentYellow = "#FFD54F",
            AccentPink = "#CE93D8",
            ButtonBg = "#21262D",
            ButtonHoverBg = "#30363D",
            ActionBg = "#238636",
            ActionHoverBg = "#2EA043",
            SeparatorColor = "#21262D",
            SidebarHoverBg = "#1C2333",
            InputBg = "#21262D",
            InputBorder = "#30363D",
            StatusBarBg = "#161B22",
            ChartGridColor = "#21262D",
            // Original overlay gradient
            OverlayGradientStart = "#1A1A2E",
            OverlayGradientMid = "#16213E",
            OverlayGradientEnd = "#0F3460",
            OverlaySeparator = "#334466",
            OverlayDownloadGlow = "#00E676",
            OverlayUploadGlow = "#42A5F5",
            OverlayDownloadUnitColor = "#80E6A0",
            OverlayUploadUnitColor = "#80B0E0"
        },
        ["Dark"] = new ThemeDefinition
        {
            Name = "Dark",
            DisplayName = "Dark",
            WindowBg = "#0D1117",
            SidebarBg = "#0D1117",
            SidebarBorder = "#21262D",
            CardBg = "#161B22",
            MiniCardBg = "#1C2333",
            TextPrimary = "#E6EDF3",
            TextSecondary = "#8B949E",
            TextMuted = "#484F58",
            AccentGreen = "#00E676",
            AccentBlue = "#42A5F5",
            AccentPurple = "#BB86FC",
            AccentOrange = "#FF7043",
            AccentYellow = "#FFD54F",
            AccentPink = "#CE93D8",
            ButtonBg = "#21262D",
            ButtonHoverBg = "#30363D",
            ActionBg = "#238636",
            ActionHoverBg = "#2EA043",
            SeparatorColor = "#21262D",
            SidebarHoverBg = "#1C2333",
            InputBg = "#21262D",
            InputBorder = "#30363D",
            StatusBarBg = "#161B22",
            ChartGridColor = "#21262D",
            OverlayGradientStart = "#0D1117",
            OverlayGradientMid = "#161B22",
            OverlayGradientEnd = "#1C2333",
            OverlaySeparator = "#30363D",
            OverlayDownloadGlow = "#00E676",
            OverlayUploadGlow = "#42A5F5",
            OverlayDownloadUnitColor = "#80E6A0",
            OverlayUploadUnitColor = "#80B0E0"
        },
        ["Light"] = new ThemeDefinition
        {
            Name = "Light",
            DisplayName = "Light",
            WindowBg = "#FFFFFF",
            SidebarBg = "#F6F8FA",
            SidebarBorder = "#D0D7DE",
            CardBg = "#F6F8FA",
            MiniCardBg = "#EAEEF2",
            TextPrimary = "#1F2328",
            TextSecondary = "#656D76",
            TextMuted = "#8C959F",
            AccentGreen = "#1A7F37",
            AccentBlue = "#0969DA",
            AccentPurple = "#8250DF",
            AccentOrange = "#BF5700",
            AccentYellow = "#9A6700",
            AccentPink = "#BF3989",
            ButtonBg = "#EAEEF2",
            ButtonHoverBg = "#D0D7DE",
            ActionBg = "#1A7F37",
            ActionHoverBg = "#2DA44E",
            SeparatorColor = "#D8DEE4",
            SidebarHoverBg = "#EAEEF2",
            InputBg = "#FFFFFF",
            InputBorder = "#D0D7DE",
            StatusBarBg = "#F6F8FA",
            ChartGridColor = "#D8DEE4",
            OverlayGradientStart = "#E8ECF0",
            OverlayGradientMid = "#F0F3F6",
            OverlayGradientEnd = "#F6F8FA",
            OverlaySeparator = "#C0C8D0",
            OverlayDownloadGlow = "#1A7F37",
            OverlayUploadGlow = "#0969DA",
            OverlayDownloadUnitColor = "#2DA44E",
            OverlayUploadUnitColor = "#218BFF"
        },
        ["Ocean"] = new ThemeDefinition
        {
            Name = "Ocean",
            DisplayName = "Ocean Blue",
            WindowBg = "#0A192F",
            SidebarBg = "#0A192F",
            SidebarBorder = "#172A45",
            CardBg = "#112240",
            MiniCardBg = "#1D3461",
            TextPrimary = "#CCD6F6",
            TextSecondary = "#8892B0",
            TextMuted = "#495670",
            AccentGreen = "#64FFDA",
            AccentBlue = "#57CBF5",
            AccentPurple = "#C792EA",
            AccentOrange = "#F78C6C",
            AccentYellow = "#FFCB6B",
            AccentPink = "#FF5370",
            ButtonBg = "#172A45",
            ButtonHoverBg = "#1D3461",
            ActionBg = "#1B6B5A",
            ActionHoverBg = "#23896F",
            SeparatorColor = "#1D3461",
            SidebarHoverBg = "#172A45",
            InputBg = "#172A45",
            InputBorder = "#1D3461",
            StatusBarBg = "#112240",
            ChartGridColor = "#1D3461",
            OverlayGradientStart = "#0A192F",
            OverlayGradientMid = "#112240",
            OverlayGradientEnd = "#172A45",
            OverlaySeparator = "#1D3461",
            OverlayDownloadGlow = "#64FFDA",
            OverlayUploadGlow = "#57CBF5",
            OverlayDownloadUnitColor = "#88FFE6",
            OverlayUploadUnitColor = "#88D8F8"
        },
        ["Sunset"] = new ThemeDefinition
        {
            Name = "Sunset",
            DisplayName = "Sunset",
            WindowBg = "#1A1020",
            SidebarBg = "#1A1020",
            SidebarBorder = "#2D1B3D",
            CardBg = "#221530",
            MiniCardBg = "#2D1B3D",
            TextPrimary = "#F5E6D3",
            TextSecondary = "#B09080",
            TextMuted = "#6D5A4A",
            AccentGreen = "#95E676",
            AccentBlue = "#7EC8E3",
            AccentPurple = "#D4A5FF",
            AccentOrange = "#FF6B35",
            AccentYellow = "#FFC857",
            AccentPink = "#FF4F79",
            ButtonBg = "#2D1B3D",
            ButtonHoverBg = "#3D2550",
            ActionBg = "#C84B31",
            ActionHoverBg = "#E05A3A",
            SeparatorColor = "#2D1B3D",
            SidebarHoverBg = "#2D1B3D",
            InputBg = "#2D1B3D",
            InputBorder = "#3D2550",
            StatusBarBg = "#221530",
            ChartGridColor = "#2D1B3D",
            OverlayGradientStart = "#1A1020",
            OverlayGradientMid = "#221530",
            OverlayGradientEnd = "#2D1B3D",
            OverlaySeparator = "#3D2550",
            OverlayDownloadGlow = "#95E676",
            OverlayUploadGlow = "#7EC8E3",
            OverlayDownloadUnitColor = "#B0F0A0",
            OverlayUploadUnitColor = "#A0D8F0"
        },
        ["Forest"] = new ThemeDefinition
        {
            Name = "Forest",
            DisplayName = "Forest",
            WindowBg = "#0B1A0F",
            SidebarBg = "#0B1A0F",
            SidebarBorder = "#1A3320",
            CardBg = "#122218",
            MiniCardBg = "#1A3320",
            TextPrimary = "#D4E8D4",
            TextSecondary = "#8BAF8B",
            TextMuted = "#4A6B4A",
            AccentGreen = "#00E676",
            AccentBlue = "#4FC3F7",
            AccentPurple = "#B39DDB",
            AccentOrange = "#FFB74D",
            AccentYellow = "#FFF176",
            AccentPink = "#F48FB1",
            ButtonBg = "#1A3320",
            ButtonHoverBg = "#254A30",
            ActionBg = "#2E7D32",
            ActionHoverBg = "#388E3C",
            SeparatorColor = "#1A3320",
            SidebarHoverBg = "#1A3320",
            InputBg = "#1A3320",
            InputBorder = "#254A30",
            StatusBarBg = "#122218",
            ChartGridColor = "#1A3320",
            OverlayGradientStart = "#0B1A0F",
            OverlayGradientMid = "#122218",
            OverlayGradientEnd = "#1A3320",
            OverlaySeparator = "#254A30",
            OverlayDownloadGlow = "#00E676",
            OverlayUploadGlow = "#4FC3F7",
            OverlayDownloadUnitColor = "#80F0B0",
            OverlayUploadUnitColor = "#80D8F8"
        },
        ["Cyberpunk"] = new ThemeDefinition
        {
            Name = "Cyberpunk",
            DisplayName = "Cyberpunk",
            WindowBg = "#0D0221",
            SidebarBg = "#0D0221",
            SidebarBorder = "#1A0A3E",
            CardBg = "#150538",
            MiniCardBg = "#1A0A3E",
            TextPrimary = "#E0E0FF",
            TextSecondary = "#9D8EC7",
            TextMuted = "#5A4D7A",
            AccentGreen = "#0FFF50",
            AccentBlue = "#00D4FF",
            AccentPurple = "#BD00FF",
            AccentOrange = "#FF6E27",
            AccentYellow = "#FFE227",
            AccentPink = "#FF2A6D",
            ButtonBg = "#1A0A3E",
            ButtonHoverBg = "#2A1560",
            ActionBg = "#6D00B3",
            ActionHoverBg = "#8800E0",
            SeparatorColor = "#2A1560",
            SidebarHoverBg = "#1A0A3E",
            InputBg = "#1A0A3E",
            InputBorder = "#2A1560",
            StatusBarBg = "#150538",
            ChartGridColor = "#2A1560",
            OverlayGradientStart = "#0D0221",
            OverlayGradientMid = "#150538",
            OverlayGradientEnd = "#1A0A3E",
            OverlaySeparator = "#2A1560",
            OverlayDownloadGlow = "#0FFF50",
            OverlayUploadGlow = "#00D4FF",
            OverlayDownloadUnitColor = "#60FF80",
            OverlayUploadUnitColor = "#60E0FF"
        }
    };

    public string CurrentThemeName { get; private set; } = "Default";

    public IReadOnlyList<string> AvailableThemes => _themes.Keys.ToList();

    public IReadOnlyDictionary<string, ThemeDefinition> ThemeDefinitions => _themes;

    public void ApplyTheme(string themeName)
    {
        if (!_themes.TryGetValue(themeName, out var theme))
            theme = _themes["Default"];

        CurrentThemeName = theme.Name;

        var res = Application.Current.Resources;
        res["WindowBg"] = BrushFromHex(theme.WindowBg);
        res["SidebarBg"] = BrushFromHex(theme.SidebarBg);
        res["SidebarBorder"] = BrushFromHex(theme.SidebarBorder);
        res["CardBg"] = BrushFromHex(theme.CardBg);
        res["MiniCardBg"] = BrushFromHex(theme.MiniCardBg);
        res["TextPrimary"] = BrushFromHex(theme.TextPrimary);
        res["TextSecondary"] = BrushFromHex(theme.TextSecondary);
        res["TextMuted"] = BrushFromHex(theme.TextMuted);
        res["AccentGreen"] = BrushFromHex(theme.AccentGreen);
        res["AccentBlue"] = BrushFromHex(theme.AccentBlue);
        res["AccentPurple"] = BrushFromHex(theme.AccentPurple);
        res["AccentOrange"] = BrushFromHex(theme.AccentOrange);
        res["AccentYellow"] = BrushFromHex(theme.AccentYellow);
        res["AccentPink"] = BrushFromHex(theme.AccentPink);
        res["ButtonBg"] = BrushFromHex(theme.ButtonBg);
        res["ButtonHoverBg"] = BrushFromHex(theme.ButtonHoverBg);
        res["ActionBg"] = BrushFromHex(theme.ActionBg);
        res["ActionHoverBg"] = BrushFromHex(theme.ActionHoverBg);
        res["SeparatorColor"] = BrushFromHex(theme.SeparatorColor);
        res["SidebarHoverBg"] = BrushFromHex(theme.SidebarHoverBg);
        res["InputBg"] = BrushFromHex(theme.InputBg);
        res["InputBorder"] = BrushFromHex(theme.InputBorder);
        res["StatusBarBg"] = BrushFromHex(theme.StatusBarBg);

        // Overlay-specific resources
        res["OverlayGradientStart"] = ColorFromHex(theme.OverlayGradientStart);
        res["OverlayGradientMid"] = ColorFromHex(theme.OverlayGradientMid);
        res["OverlayGradientEnd"] = ColorFromHex(theme.OverlayGradientEnd);
        res["OverlaySeparator"] = BrushFromHex(theme.OverlaySeparator);
        res["OverlayDownloadColor"] = BrushFromHex(theme.OverlayDownloadGlow);
        res["OverlayUploadColor"] = BrushFromHex(theme.OverlayUploadGlow);
        res["OverlayDownloadGlowColor"] = ColorFromHex(theme.OverlayDownloadGlow);
        res["OverlayUploadGlowColor"] = ColorFromHex(theme.OverlayUploadGlow);
        res["OverlayDownloadUnitColor"] = BrushFromHex(theme.OverlayDownloadUnitColor);
        res["OverlayUploadUnitColor"] = BrushFromHex(theme.OverlayUploadUnitColor);
    }

    private static SolidColorBrush BrushFromHex(string hex)
    {
        var color = (Color)ColorConverter.ConvertFromString(hex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color ColorFromHex(string hex)
    {
        return (Color)ColorConverter.ConvertFromString(hex);
    }
}
