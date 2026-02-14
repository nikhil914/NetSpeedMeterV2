using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.Defaults;
using SkiaSharp;
using NetMonitorPro.Core.Interfaces;
using NetMonitorPro.Core.Models;
using NetMonitorPro.Core.Services;
using NetMonitorPro.Data.Repositories;

namespace NetMonitorPro.App.ViewModels;

/// <summary>
/// ViewModel for the main dashboard window.
/// Manages real-time speed chart, daily usage bar chart, network info, and navigation.
/// </summary>
public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly INetworkMonitorService _monitor;
    private readonly ISettingsService _settings;
    private readonly DatabaseService _database;
    private readonly SpeedTestService _speedTest;
    private bool _disposed;
    private CancellationTokenSource? _speedTestCts;

    // Speed history for real-time chart (last 60 data points)
    private readonly ObservableCollection<ObservableValue> _downloadSeries = new();
    private readonly ObservableCollection<ObservableValue> _uploadSeries = new();
    private int _tick;

    [ObservableProperty]
    private string _currentPage = "Overview";

    [ObservableProperty]
    private string _downloadSpeed = "0 B/s";

    [ObservableProperty]
    private string _uploadSpeed = "0 B/s";

    [ObservableProperty]
    private string _sessionDownloaded = "0 MB";

    [ObservableProperty]
    private string _sessionUploaded = "0 MB";

    [ObservableProperty]
    private ObservableCollection<AdapterInfo> _adapters = new();

    // Real-time speed chart
    public ISeries[] SpeedSeries { get; }
    public Axis[] SpeedXAxes { get; }
    public Axis[] SpeedYAxes { get; }

    // Daily usage chart
    [ObservableProperty]
    private ISeries[]? _dailyUsageSeries;

    [ObservableProperty]
    private Axis[]? _dailyXAxes;

    [ObservableProperty]
    private Axis[]? _dailyYAxes;

    private long _sessionTotalDown;
    private long _sessionTotalUp;

    // ── Settings properties ──
    [ObservableProperty]
    private bool _alwaysOnTop;

    [ObservableProperty]
    private bool _startMinimized;

    [ObservableProperty]
    private bool _launchOnStartup;

    [ObservableProperty]
    private bool _clickThroughEnabled;

    [ObservableProperty]
    private int _updateIntervalMs;

    [ObservableProperty]
    private double _backgroundOpacity;

    [ObservableProperty]
    private bool _alertOnSpeedDrop;

    [ObservableProperty]
    private double _speedDropThresholdMbps;

    [ObservableProperty]
    private bool _alertOnDataCap;

    [ObservableProperty]
    private double _dailyDataCapGB;

    [ObservableProperty]
    private bool _alertOnDisconnect;

    [ObservableProperty]
    private string _selectedAdapterName = "Auto";

    [ObservableProperty]
    private ObservableCollection<string> _availableAdapters = new();

    [ObservableProperty]
    private string _settingsStatus = "";

    // ── Speed test properties ──
    [ObservableProperty]
    private string _speedTestStatus = "Ready to test";

    [ObservableProperty]
    private int _speedTestProgress;

    [ObservableProperty]
    private bool _isSpeedTestRunning;

    [ObservableProperty]
    private string _speedTestDownload = "—";

    [ObservableProperty]
    private string _speedTestUpload = "—";

    [ObservableProperty]
    private string _speedTestLatency = "—";

    public DashboardViewModel(INetworkMonitorService monitor, ISettingsService settings, 
                               DatabaseService database, SpeedTestService speedTest)
    {
        _monitor = monitor;
        _settings = settings;
        _database = database;
        _speedTest = speedTest;

        // Initialize real-time speed chart with 60 empty points
        for (int i = 0; i < 60; i++)
        {
            _downloadSeries.Add(new ObservableValue(0));
            _uploadSeries.Add(new ObservableValue(0));
        }

        SpeedSeries = new ISeries[]
        {
            new LineSeries<ObservableValue>
            {
                Name = "Download",
                Values = _downloadSeries,
                Stroke = new SolidColorPaint(new SKColor(0, 230, 118), 2),
                Fill = new SolidColorPaint(new SKColor(0, 230, 118, 40)),
                GeometrySize = 0,
                LineSmoothness = 0.5
            },
            new LineSeries<ObservableValue>
            {
                Name = "Upload",
                Values = _uploadSeries,
                Stroke = new SolidColorPaint(new SKColor(66, 165, 245), 2),
                Fill = new SolidColorPaint(new SKColor(66, 165, 245, 40)),
                GeometrySize = 0,
                LineSmoothness = 0.5
            }
        };

        SpeedXAxes = new Axis[]
        {
            new Axis
            {
                Name = "Time (seconds ago)",
                LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 200)),
                NamePaint = new SolidColorPaint(new SKColor(180, 180, 200)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(50, 50, 70)),
                Labeler = val => $"-{60 - (int)val}s"
            }
        };

        SpeedYAxes = new Axis[]
        {
            new Axis
            {
                Name = "Speed (MB/s)",
                LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 200)),
                NamePaint = new SolidColorPaint(new SKColor(180, 180, 200)),
                SeparatorsPaint = new SolidColorPaint(new SKColor(50, 50, 70)),
                MinLimit = 0,
                Labeler = val => $"{val:F1}"
            }
        };

        _monitor.StatsUpdated += OnStatsUpdated;

        // Load current settings
        LoadSettingsFromModel();
    }

    private void LoadSettingsFromModel()
    {
        var s = _settings.Settings;
        AlwaysOnTop = s.AlwaysOnTop;
        StartMinimized = s.StartMinimized;
        LaunchOnStartup = s.LaunchOnStartup;
        ClickThroughEnabled = s.ClickThroughEnabled;
        UpdateIntervalMs = s.UpdateIntervalMs;
        BackgroundOpacity = s.BackgroundOpacity;
        AlertOnSpeedDrop = s.AlertOnSpeedDrop;
        SpeedDropThresholdMbps = s.SpeedDropThresholdMbps;
        AlertOnDataCap = s.AlertOnDataCap;
        DailyDataCapGB = s.DailyDataCapGB;
        AlertOnDisconnect = s.AlertOnDisconnect;
        SelectedAdapterName = s.SelectedAdapterName ?? "Auto";
    }

    private void OnStatsUpdated(object? sender, NetworkStats stats)
    {
        _tick++;

        // Update speed display
        DownloadSpeed = stats.FormattedDownloadSpeed;
        UploadSpeed = stats.FormattedUploadSpeed;

        // Accumulate session totals
        _sessionTotalDown += stats.DownloadBytesPerSecond;
        _sessionTotalUp += stats.UploadBytesPerSecond;
        SessionDownloaded = FormatBytes(_sessionTotalDown);
        SessionUploaded = FormatBytes(_sessionTotalUp);

        // Push to real-time chart (shift left, add new point)
        var dlMBps = stats.DownloadBytesPerSecond / (1024.0 * 1024.0);
        var ulMBps = stats.UploadBytesPerSecond / (1024.0 * 1024.0);

        _downloadSeries.RemoveAt(0);
        _downloadSeries.Add(new ObservableValue(dlMBps));
        _uploadSeries.RemoveAt(0);
        _uploadSeries.Add(new ObservableValue(ulMBps));
    }

    [RelayCommand]
    private void NavigateTo(string page)
    {
        CurrentPage = page;

        if (page == "Network")
            LoadAdapters();
        else if (page == "Settings")
            LoadAvailableAdapters();
    }

    private void LoadAvailableAdapters()
    {
        try
        {
            var adapters = _monitor.GetAdapters();
            var names = new ObservableCollection<string> { "Auto" };
            foreach (var a in adapters)
                names.Add(a.Name);
            AvailableAdapters = names;
        }
        catch { AvailableAdapters = new ObservableCollection<string> { "Auto" }; }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var s = _settings.Settings;
        s.AlwaysOnTop = AlwaysOnTop;
        s.StartMinimized = StartMinimized;
        s.LaunchOnStartup = LaunchOnStartup;
        s.ClickThroughEnabled = ClickThroughEnabled;
        s.UpdateIntervalMs = UpdateIntervalMs;
        s.BackgroundOpacity = BackgroundOpacity;
        s.AlertOnSpeedDrop = AlertOnSpeedDrop;
        s.SpeedDropThresholdMbps = SpeedDropThresholdMbps;
        s.AlertOnDataCap = AlertOnDataCap;
        s.DailyDataCapGB = DailyDataCapGB;
        s.AlertOnDisconnect = AlertOnDisconnect;
        s.SelectedAdapterName = SelectedAdapterName == "Auto" ? null : SelectedAdapterName;
        _settings.Save();
        SettingsStatus = "✓ Settings saved";
    }

    [RelayCommand]
    private void ResetSettings()
    {
        // Reset the current settings to defaults
        var fresh = new AppSettings();
        var s = _settings.Settings;
        s.AlwaysOnTop = fresh.AlwaysOnTop;
        s.StartMinimized = fresh.StartMinimized;
        s.LaunchOnStartup = fresh.LaunchOnStartup;
        s.ClickThroughEnabled = fresh.ClickThroughEnabled;
        s.UpdateIntervalMs = fresh.UpdateIntervalMs;
        s.BackgroundOpacity = fresh.BackgroundOpacity;
        s.AlertOnSpeedDrop = fresh.AlertOnSpeedDrop;
        s.SpeedDropThresholdMbps = fresh.SpeedDropThresholdMbps;
        s.AlertOnDataCap = fresh.AlertOnDataCap;
        s.DailyDataCapGB = fresh.DailyDataCapGB;
        s.AlertOnDisconnect = fresh.AlertOnDisconnect;
        s.SelectedAdapterName = fresh.SelectedAdapterName;
        _settings.Save();
        LoadSettingsFromModel();
        SettingsStatus = "Settings reset to defaults";
    }

    [RelayCommand]
    private async Task LoadDailyUsageAsync()
    {
        try
        {
            await _database.InitializeAsync();
            var data = await _database.GetDailyUsageAsync(DateTime.Today.AddDays(-30), DateTime.Today);

            var dlValues = new ObservableCollection<double>();
            var ulValues = new ObservableCollection<double>();
            var labels = new List<string>();

            foreach (var day in data)
            {
                dlValues.Add(day.BytesDownloaded / (1024.0 * 1024.0 * 1024.0)); // GB
                ulValues.Add(day.BytesUploaded / (1024.0 * 1024.0 * 1024.0));
                labels.Add(DateTime.Parse(day.Date).ToString("MMM dd"));
            }

            DailyUsageSeries = new ISeries[]
            {
                new ColumnSeries<double>
                {
                    Name = "Download (GB)",
                    Values = dlValues,
                    Fill = new SolidColorPaint(new SKColor(0, 230, 118, 200))
                },
                new ColumnSeries<double>
                {
                    Name = "Upload (GB)",
                    Values = ulValues,
                    Fill = new SolidColorPaint(new SKColor(66, 165, 245, 200))
                }
            };

            DailyXAxes = new Axis[]
            {
                new Axis
                {
                    Labels = labels,
                    LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 200)),
                    LabelsRotation = 45
                }
            };

            DailyYAxes = new Axis[]
            {
                new Axis
                {
                    Name = "GB",
                    LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 200)),
                    NamePaint = new SolidColorPaint(new SKColor(180, 180, 200)),
                    MinLimit = 0
                }
            };
        }
        catch
        {
            // Database not yet populated — ignore
        }
    }

    [RelayCommand]
    private void LoadAdapters()
    {
        try
        {
            Adapters = new ObservableCollection<AdapterInfo>(_monitor.GetAdapters());
        }
        catch
        {
            Adapters = new ObservableCollection<AdapterInfo>();
        }
    }

    private static string FormatBytes(long bytes)
    {
        const double KB = 1024.0;
        const double MB = KB * 1024;
        const double GB = MB * 1024;

        return bytes switch
        {
            >= (long)GB => $"{bytes / GB:F2} GB",
            >= (long)MB => $"{bytes / MB:F2} MB",
            >= (long)KB => $"{bytes / KB:F2} KB",
            _ => $"{bytes} B"
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _monitor.StatsUpdated -= OnStatsUpdated;
        _speedTestCts?.Cancel();
        _speedTestCts?.Dispose();
    }

    [RelayCommand]
    private async Task RunSpeedTestAsync()
    {
        if (IsSpeedTestRunning) return;

        IsSpeedTestRunning = true;
        SpeedTestDownload = "...";
        SpeedTestUpload = "...";
        SpeedTestLatency = "...";
        SpeedTestProgress = 0;

        _speedTestCts = new CancellationTokenSource();

        _speedTest.Progress += OnSpeedTestProgress;

        try
        {
            var result = await _speedTest.RunAsync(_speedTestCts.Token);
            SpeedTestDownload = $"{result.DownloadMbps:F2} Mbps";
            SpeedTestUpload = $"{result.UploadMbps:F2} Mbps";
            SpeedTestLatency = result.LatencyMs >= 0 ? $"{result.LatencyMs:F0} ms" : "N/A";
            SpeedTestStatus = $"Test completed at {result.Timestamp:HH:mm:ss}";

            // Save to database
            try
            {
                await _database.SaveSpeedTestAsync(new Data.Entities.SpeedTestRecord
                {
                    Timestamp = result.Timestamp.ToString("o"),
                    DownloadSpeedMbps = result.DownloadMbps,
                    UploadSpeedMbps = result.UploadMbps,
                    LatencyMs = (int)result.LatencyMs,
                    TestServer = "Cloudflare"
                });
            }
            catch { /* DB save failed — not critical */ }
        }
        catch (OperationCanceledException)
        {
            SpeedTestStatus = "Test cancelled";
        }
        catch (Exception ex)
        {
            SpeedTestStatus = $"Test failed: {ex.Message}";
        }
        finally
        {
            _speedTest.Progress -= OnSpeedTestProgress;
            IsSpeedTestRunning = false;
            SpeedTestProgress = 100;
        }
    }

    [RelayCommand]
    private void CancelSpeedTest()
    {
        _speedTestCts?.Cancel();
    }

    private void OnSpeedTestProgress(string phase, int percent, double speedMbps)
    {
        SpeedTestStatus = phase;
        SpeedTestProgress = percent;
    }
}
