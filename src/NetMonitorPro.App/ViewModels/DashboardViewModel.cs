using System.Collections.ObjectModel;
using System.Windows.Threading;
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
using NetMonitorPro.App.Services;
using NetMonitorPro.Data.Repositories;
using NetMonitorPro.Data.Entities;

namespace NetMonitorPro.App.ViewModels;

/// <summary>
/// ViewModel for the main dashboard window.
/// Manages real-time speed chart, comprehensive usage statistics, network info, and navigation.
/// </summary>
public partial class DashboardViewModel : ObservableObject, IDisposable
{
    private readonly INetworkMonitorService _monitor;
    private readonly ISettingsService _settings;
    private readonly DatabaseService _database;
    private readonly SpeedTestService _speedTest;
    private readonly ThemeService _themeService;
    private bool _disposed;
    private CancellationTokenSource? _speedTestCts;
    private DispatcherTimer? _usageRefreshTimer;

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

    // ── Usage Statistics ──

    [ObservableProperty]
    private string _selectedTimeRange = "Today";

    [ObservableProperty]
    private string _todayDownloaded = "—";

    [ObservableProperty]
    private string _todayUploaded = "—";

    [ObservableProperty]
    private string _todayPeakDown = "—";

    [ObservableProperty]
    private string _todayPeakUp = "—";

    [ObservableProperty]
    private string _weeklyDownloaded = "—";

    [ObservableProperty]
    private string _weeklyUploaded = "—";

    [ObservableProperty]
    private string _monthlyDownloaded = "—";

    [ObservableProperty]
    private string _monthlyUploaded = "—";

    [ObservableProperty]
    private string _avgDailyUsage = "—";

    // Usage chart (switches between hourly/daily based on time range)
    [ObservableProperty]
    private ISeries[]? _usageChartSeries;

    [ObservableProperty]
    private Axis[]? _usageChartXAxes;

    [ObservableProperty]
    private Axis[]? _usageChartYAxes;

    [ObservableProperty]
    private string _usageChartTitle = "Today's Usage by Hour";

    // Top apps
    [ObservableProperty]
    private ObservableCollection<AppUsageItem> _topApps = new();

    [ObservableProperty]
    private string _topAppsTitle = "Top Applications — Today";

    // Session history
    [ObservableProperty]
    private ObservableCollection<SessionDisplayItem> _recentSessions = new();

    // Network events log
    [ObservableProperty]
    private ObservableCollection<EventDisplayItem> _recentEvents = new();

    // Speed test history
    [ObservableProperty]
    private ObservableCollection<SpeedTestDisplayItem> _speedTestHistory = new();

    // Daily usage chart (always shows last 30 days)
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

    // ── Theme properties ──
    [ObservableProperty]
    private string _selectedTheme = "Dark";

    [ObservableProperty]
    private ObservableCollection<string> _availableThemes = new();

    // ── Speed unit properties ──
    [ObservableProperty]
    private bool _speedUnitBits;

    [ObservableProperty]
    private string _speedUnitLabel = "Bytes/s";

    public DashboardViewModel(INetworkMonitorService monitor, ISettingsService settings, 
                               DatabaseService database, SpeedTestService speedTest,
                               ThemeService themeService)
    {
        _monitor = monitor;
        _settings = settings;
        _database = database;
        _speedTest = speedTest;
        _themeService = themeService;

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

        // Load available themes
        AvailableThemes = new ObservableCollection<string>(_themeService.AvailableThemes);
        SelectedTheme = _settings.Settings.SelectedTheme;

        // Load speed unit setting
        SpeedUnitBits = _settings.Settings.SpeedUnitBits;
        SpeedUnitLabel = SpeedUnitBits ? "Bits/s" : "Bytes/s";

        // Auto-refresh timer for usage page (every 10 seconds)
        _usageRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(10)
        };
        _usageRefreshTimer.Tick += async (_, _) =>
        {
            if (CurrentPage == "Usage")
            {
                await LoadSummaryCardsAsync();
            }
        };
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

        // Update speed display (respects bits/bytes unit setting)
        DownloadSpeed = NetworkStats.FormatSpeed(stats.DownloadBytesPerSecond, SpeedUnitBits);
        UploadSpeed = NetworkStats.FormatSpeed(stats.UploadBytesPerSecond, SpeedUnitBits);

        // Accumulate session totals (multiply by interval to get actual bytes transferred)
        var intervalSec = (_settings.Settings.UpdateIntervalMs) / 1000.0;
        _sessionTotalDown += (long)(stats.DownloadBytesPerSecond * intervalSec);
        _sessionTotalUp += (long)(stats.UploadBytesPerSecond * intervalSec);
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
        else if (page == "Usage")
        {
            _ = LoadUsageDataAsync();
            _usageRefreshTimer?.Start();
        }
        else
        {
            _usageRefreshTimer?.Stop();
        }
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
        s.SelectedTheme = SelectedTheme;
        s.SpeedUnitBits = SpeedUnitBits;
        _settings.Save();
        SettingsStatus = "✓ Settings saved";
    }

    [RelayCommand]
    private void ResetSettings()
    {
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
        s.SelectedTheme = fresh.SelectedTheme;
        s.SpeedUnitBits = fresh.SpeedUnitBits;
        _settings.Save();
        LoadSettingsFromModel();
        SelectedTheme = fresh.SelectedTheme;
        _themeService.ApplyTheme(fresh.SelectedTheme);
        SpeedUnitBits = fresh.SpeedUnitBits;
        SpeedUnitLabel = SpeedUnitBits ? "Bits/s" : "Bytes/s";
        SettingsStatus = "Settings reset to defaults";
    }

    [RelayCommand]
    private void ApplyTheme(string themeName)
    {
        SelectedTheme = themeName;
        _themeService.ApplyTheme(themeName);
        _settings.Settings.SelectedTheme = themeName;
        _settings.Save();
    }

    [RelayCommand]
    private void ToggleSpeedUnit()
    {
        SpeedUnitBits = !SpeedUnitBits;
        SpeedUnitLabel = SpeedUnitBits ? "Bits/s" : "Bytes/s";
        _settings.Settings.SpeedUnitBits = SpeedUnitBits;
        _settings.Save();
    }

    // ════════════════════════════════════════════
    //  USAGE DATA LOADING — Complete Statistics
    // ════════════════════════════════════════════

    [RelayCommand]
    private async Task LoadUsageDataAsync()
    {
        await _database.InitializeAsync();
        await LoadSummaryCardsAsync();
        await LoadUsageChartAsync();
        await LoadTopAppsAsync();
        await LoadRecentSessionsAsync();
        await LoadRecentEventsAsync();
        await LoadSpeedTestHistoryAsync();
    }

    [RelayCommand]
    private async Task SwitchTimeRange(string range)
    {
        SelectedTimeRange = range;
        await LoadSummaryCardsAsync();
        await LoadUsageChartAsync();
        await LoadTopAppsAsync();
    }

    private async Task LoadSummaryCardsAsync()
    {
        try
        {
            var today = DateTime.Today.ToString("yyyy-MM-dd");
            var (dayDown, dayUp, peakD, peakU) = await _database.GetDaySummaryAsync(today);

            TodayDownloaded = FormatBytes(dayDown);
            TodayUploaded = FormatBytes(dayUp);
            TodayPeakDown = NetworkStats.FormatSpeed(peakD);
            TodayPeakUp = NetworkStats.FormatSpeed(peakU);

            // Weekly totals
            var weekData = await _database.GetWeeklyUsageAsync(DateTime.Today);
            var weekDown = weekData.Sum(d => d.BytesDownloaded);
            var weekUp = weekData.Sum(d => d.BytesUploaded);
            WeeklyDownloaded = FormatBytes(weekDown);
            WeeklyUploaded = FormatBytes(weekUp);

            // Monthly totals
            var (monthDown, monthUp, _, _) = await _database.GetMonthSummaryAsync(DateTime.Now.Year, DateTime.Now.Month);
            MonthlyDownloaded = FormatBytes(monthDown);
            MonthlyUploaded = FormatBytes(monthUp);

            // Average daily usage this month
            var daysInMonth = DateTime.Now.Day;
            if (daysInMonth > 0 && (monthDown + monthUp) > 0)
            {
                var avgDay = (monthDown + monthUp) / daysInMonth;
                AvgDailyUsage = FormatBytes(avgDay);
            }
            else
            {
                AvgDailyUsage = "—";
            }
        }
        catch
        {
            TodayDownloaded = "—";
            TodayUploaded = "—";
            TodayPeakDown = "—";
            TodayPeakUp = "—";
        }
    }

    private async Task LoadUsageChartAsync()
    {
        try
        {
            switch (SelectedTimeRange)
            {
                case "Today":
                    await LoadHourlyChartAsync();
                    break;
                case "Week":
                    await LoadWeeklyChartAsync();
                    break;
                case "Month":
                    await LoadMonthlyChartAsync();
                    break;
            }
        }
        catch { /* Chart load failed */ }
    }

    private async Task LoadHourlyChartAsync()
    {
        UsageChartTitle = "Today's Usage by Hour";

        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var hourlyData = await _database.GetHourlyUsageAsync(today);

        var dlValues = new double[24];
        var ulValues = new double[24];

        foreach (var h in hourlyData)
        {
            if (h.Hour >= 0 && h.Hour < 24)
            {
                dlValues[h.Hour] += h.BytesDownloaded / (1024.0 * 1024.0); // MB
                ulValues[h.Hour] += h.BytesUploaded / (1024.0 * 1024.0);
            }
        }

        var labels = Enumerable.Range(0, 24).Select(h => $"{h:D2}:00").ToList();

        UsageChartSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = "Download (MB)",
                Values = dlValues,
                Fill = new SolidColorPaint(new SKColor(0, 230, 118, 200)),
                MaxBarWidth = 12
            },
            new ColumnSeries<double>
            {
                Name = "Upload (MB)",
                Values = ulValues,
                Fill = new SolidColorPaint(new SKColor(66, 165, 245, 200)),
                MaxBarWidth = 12
            }
        };

        UsageChartXAxes = new Axis[]
        {
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 200)),
                LabelsRotation = 45,
                TextSize = 10
            }
        };

        UsageChartYAxes = new Axis[]
        {
            new Axis
            {
                Name = "MB",
                LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 200)),
                NamePaint = new SolidColorPaint(new SKColor(180, 180, 200)),
                MinLimit = 0
            }
        };
    }

    private async Task LoadWeeklyChartAsync()
    {
        UsageChartTitle = "This Week's Usage by Day";

        var data = await _database.GetWeeklyUsageAsync(DateTime.Today);

        // Ensure we have 7 days (fill gaps)
        var startDate = DateTime.Today.AddDays(-6);
        var dlValues = new List<double>();
        var ulValues = new List<double>();
        var labels = new List<string>();

        for (int i = 0; i < 7; i++)
        {
            var date = startDate.AddDays(i);
            var dateStr = date.ToString("yyyy-MM-dd");
            var dayData = data.Where(d => d.Date == dateStr);
            dlValues.Add(dayData.Sum(d => d.BytesDownloaded) / (1024.0 * 1024.0 * 1024.0)); // GB
            ulValues.Add(dayData.Sum(d => d.BytesUploaded) / (1024.0 * 1024.0 * 1024.0));
            labels.Add(date.ToString("ddd\nMMM dd"));
        }

        UsageChartSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = "Download (GB)",
                Values = dlValues,
                Fill = new SolidColorPaint(new SKColor(0, 230, 118, 200)),
                MaxBarWidth = 30
            },
            new ColumnSeries<double>
            {
                Name = "Upload (GB)",
                Values = ulValues,
                Fill = new SolidColorPaint(new SKColor(66, 165, 245, 200)),
                MaxBarWidth = 30
            }
        };

        UsageChartXAxes = new Axis[]
        {
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 200)),
                TextSize = 11
            }
        };

        UsageChartYAxes = new Axis[]
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

    private async Task LoadMonthlyChartAsync()
    {
        UsageChartTitle = $"This Month's Usage ({DateTime.Now:MMMM yyyy})";

        var from = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var to = DateTime.Today;
        var data = await _database.GetDailyUsageAsync(from, to);

        var daysInMonth = DateTime.DaysInMonth(DateTime.Now.Year, DateTime.Now.Month);
        var dlValues = new List<double>();
        var ulValues = new List<double>();
        var labels = new List<string>();

        for (int day = 1; day <= Math.Min(daysInMonth, to.Day); day++)
        {
            var date = new DateTime(DateTime.Now.Year, DateTime.Now.Month, day);
            var dateStr = date.ToString("yyyy-MM-dd");
            var dayData = data.Where(d => d.Date == dateStr);
            dlValues.Add(dayData.Sum(d => d.BytesDownloaded) / (1024.0 * 1024.0 * 1024.0)); // GB
            ulValues.Add(dayData.Sum(d => d.BytesUploaded) / (1024.0 * 1024.0 * 1024.0));
            labels.Add(date.ToString("dd"));
        }

        UsageChartSeries = new ISeries[]
        {
            new ColumnSeries<double>
            {
                Name = "Download (GB)",
                Values = dlValues,
                Fill = new SolidColorPaint(new SKColor(0, 230, 118, 200)),
                MaxBarWidth = 16
            },
            new ColumnSeries<double>
            {
                Name = "Upload (GB)",
                Values = ulValues,
                Fill = new SolidColorPaint(new SKColor(66, 165, 245, 200)),
                MaxBarWidth = 16
            }
        };

        UsageChartXAxes = new Axis[]
        {
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(new SKColor(180, 180, 200)),
                TextSize = 10
            }
        };

        UsageChartYAxes = new Axis[]
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

    private async Task LoadTopAppsAsync()
    {
        try
        {
            List<ProcessHistory> data;
            var today = DateTime.Today.ToString("yyyy-MM-dd");

            switch (SelectedTimeRange)
            {
                case "Today":
                    data = await _database.GetProcessUsageAsync(today);
                    TopAppsTitle = "Top Applications — Today";
                    break;
                case "Week":
                    var weekFrom = DateTime.Today.AddDays(-6).ToString("yyyy-MM-dd");
                    data = await _database.GetProcessUsageRangeAsync(weekFrom, today);
                    TopAppsTitle = "Top Applications — This Week";
                    break;
                case "Month":
                    var monthFrom = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).ToString("yyyy-MM-dd");
                    data = await _database.GetProcessUsageRangeAsync(monthFrom, today);
                    TopAppsTitle = "Top Applications — This Month";
                    break;
                default:
                    data = await _database.GetProcessUsageAsync(today);
                    TopAppsTitle = "Top Applications — Today";
                    break;
            }

            var totalBytes = data.Sum(d => d.TotalBytesDownloaded + d.TotalBytesUploaded);

            TopApps = new ObservableCollection<AppUsageItem>(
                data.Take(15).Select(p => new AppUsageItem
                {
                    ProcessName = p.ProcessName,
                    Downloaded = FormatBytes(p.TotalBytesDownloaded),
                    Uploaded = FormatBytes(p.TotalBytesUploaded),
                    Total = FormatBytes(p.TotalBytesDownloaded + p.TotalBytesUploaded),
                    Percentage = totalBytes > 0 
                        ? (double)(p.TotalBytesDownloaded + p.TotalBytesUploaded) / totalBytes * 100 
                        : 0
                }));
        }
        catch
        {
            TopApps = new ObservableCollection<AppUsageItem>();
        }
    }

    private async Task LoadRecentSessionsAsync()
    {
        try
        {
            var sessions = await _database.GetRecentSessionsAsync(10);
            RecentSessions = new ObservableCollection<SessionDisplayItem>(
                sessions.Select(s =>
                {
                    var start = DateTime.TryParse(s.SessionStart, out var st) ? st : DateTime.MinValue;
                    var end = s.SessionEnd != null && DateTime.TryParse(s.SessionEnd, out var en) ? en : (DateTime?)null;
                    var duration = end.HasValue ? end.Value - start : DateTime.Now - start;

                    return new SessionDisplayItem
                    {
                        StartTime = start.ToString("MMM dd, HH:mm"),
                        EndTime = end?.ToString("HH:mm") ?? "Active",
                        Duration = FormatDuration(duration),
                        DataTransferred = FormatBytes(s.BytesDownloaded + s.BytesUploaded),
                        Downloaded = FormatBytes(s.BytesDownloaded),
                        Uploaded = FormatBytes(s.BytesUploaded),
                        AvgDown = NetworkStats.FormatSpeed(s.AverageDownloadSpeed),
                        AvgUp = NetworkStats.FormatSpeed(s.AverageUploadSpeed),
                        IsActive = s.SessionEnd == null
                    };
                }));
        }
        catch
        {
            RecentSessions = new ObservableCollection<SessionDisplayItem>();
        }
    }

    private async Task LoadRecentEventsAsync()
    {
        try
        {
            var events = await _database.GetRecentEventsAsync(20);
            RecentEvents = new ObservableCollection<EventDisplayItem>(
                events.Select(ev =>
                {
                    var ts = DateTime.TryParse(ev.Timestamp, out var t) ? t : DateTime.MinValue;
                    return new EventDisplayItem
                    {
                        Timestamp = ts.ToString("MMM dd HH:mm:ss"),
                        EventType = ev.EventType,
                        Description = ev.Description ?? "",
                        Severity = ev.Severity,
                        SeverityColor = ev.Severity switch
                        {
                            "CRITICAL" => "#FF5252",
                            "WARNING" => "#FF7043",
                            _ => "#4CAF50"
                        }
                    };
                }));
        }
        catch
        {
            RecentEvents = new ObservableCollection<EventDisplayItem>();
        }
    }

    private async Task LoadSpeedTestHistoryAsync()
    {
        try
        {
            var tests = await _database.GetSpeedTestHistoryAsync(10);
            SpeedTestHistory = new ObservableCollection<SpeedTestDisplayItem>(
                tests.Select(t =>
                {
                    var ts = DateTime.TryParse(t.Timestamp, out var dt) ? dt : DateTime.MinValue;
                    return new SpeedTestDisplayItem
                    {
                        Date = ts.ToString("MMM dd, HH:mm"),
                        Download = $"{t.DownloadSpeedMbps:F2} Mbps",
                        Upload = $"{t.UploadSpeedMbps:F2} Mbps",
                        Latency = t.LatencyMs >= 0 ? $"{t.LatencyMs} ms" : "N/A",
                        Server = t.TestServer ?? "—"
                    };
                }));
        }
        catch
        {
            SpeedTestHistory = new ObservableCollection<SpeedTestDisplayItem>();
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

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1)
            return $"{(int)duration.TotalDays}d {duration.Hours}h {duration.Minutes}m";
        if (duration.TotalHours >= 1)
            return $"{(int)duration.TotalHours}h {duration.Minutes}m";
        return $"{(int)duration.TotalMinutes}m";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _usageRefreshTimer?.Stop();
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
                await _database.SaveSpeedTestAsync(new SpeedTestRecord
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

// ════════════════════════════════════════════
//  Display Model Classes
// ════════════════════════════════════════════

public class AppUsageItem
{
    public string ProcessName { get; set; } = string.Empty;
    public string Downloaded { get; set; } = "0 B";
    public string Uploaded { get; set; } = "0 B";
    public string Total { get; set; } = "0 B";
    public double Percentage { get; set; }
}

public class SessionDisplayItem
{
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string DataTransferred { get; set; } = "0 B";
    public string Downloaded { get; set; } = "0 B";
    public string Uploaded { get; set; } = "0 B";
    public string AvgDown { get; set; } = "0 B/s";
    public string AvgUp { get; set; } = "0 B/s";
    public bool IsActive { get; set; }
}

public class EventDisplayItem
{
    public string Timestamp { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = "INFO";
    public string SeverityColor { get; set; } = "#4CAF50";
}

public class SpeedTestDisplayItem
{
    public string Date { get; set; } = string.Empty;
    public string Download { get; set; } = "—";
    public string Upload { get; set; } = "—";
    public string Latency { get; set; } = "—";
    public string Server { get; set; } = "—";
}
