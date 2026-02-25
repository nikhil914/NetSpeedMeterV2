using Newtonsoft.Json;
using Microsoft.Win32;
using System.Runtime.Versioning;
using NetMonitorPro.Core.Interfaces;
using NetMonitorPro.Core.Models;

namespace NetMonitorPro.Core.Services;

/// <summary>
/// Persists application settings as a JSON file in %LOCALAPPDATA%/NetMonitorPro/.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class SettingsService : ISettingsService
{
    private const string AutoStartRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AutoStartValueName = "NetMonitorPro";

    private static readonly string SettingsDir =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NetMonitorPro");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "settings.json");

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonConvert.DeserializeObject<AppSettings>(json) ?? new AppSettings();
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonConvert.SerializeObject(Settings, Formatting.Indented);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Log or ignore write failures silently (read-only filesystem, etc.)
        }

        ApplyAutoStart(Settings.LaunchOnStartup);
    }

    /// <summary>
    /// Writes or removes the registry Run key so the app starts with Windows.
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static void ApplyAutoStart(bool enable)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(AutoStartRegistryKey, writable: true);
            if (key == null) return;

            if (enable)
            {
                var exePath = Environment.ProcessPath ?? 
                    System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
                if (!string.IsNullOrEmpty(exePath))
                    key.SetValue(AutoStartValueName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AutoStartValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Registry access may fail in restricted environments — ignore
        }
    }
}

