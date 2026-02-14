using Newtonsoft.Json;
using NetMonitorPro.Core.Interfaces;
using NetMonitorPro.Core.Models;

namespace NetMonitorPro.Core.Services;

/// <summary>
/// Persists application settings as a JSON file in %LOCALAPPDATA%/NetMonitorPro/.
/// </summary>
public sealed class SettingsService : ISettingsService
{
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
    }
}
