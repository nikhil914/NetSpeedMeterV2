using NetMonitorPro.Core.Models;

namespace NetMonitorPro.Core.Interfaces;

/// <summary>
/// Contract for loading/saving application settings.
/// </summary>
public interface ISettingsService
{
    AppSettings Settings { get; }
    void Load();
    void Save();
}
