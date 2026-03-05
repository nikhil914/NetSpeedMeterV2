using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using NetMonitorPro.App.ViewModels;
using NetMonitorPro.App.Views;
using NetMonitorPro.Core.Interfaces;
using NetMonitorPro.Core.Models;
using NetMonitorPro.Core.Services;
using NetMonitorPro.App.Services;
using NetMonitorPro.Data.Repositories;
using H.NotifyIcon;

namespace NetMonitorPro.App;

/// <summary>
/// Application entry point. Sets up dependency injection and launches the overlay.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? _serviceProvider;
    private TaskbarIcon? _trayIcon;
    private OverlayWindow? _overlayWindow;
    private DashboardWindow? _dashboardWindow;
    private System.Windows.Controls.MenuItem? _showHideMenuItem;

    // Usage history tracking
    private DispatcherTimer? _usageFlushTimer;
    private long _accumulatedDown;
    private long _accumulatedUp;
    private long _peakDown;
    private long _peakUp;
    private readonly object _usageLock = new();

    // Session tracking
    private int _currentSessionId;
    private long _sessionTotalDown;
    private long _sessionTotalUp;
    private int _sessionTickCount;

    public static ServiceProvider? Services { get; private set; }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Build DI container
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        Services = _serviceProvider;

        // Load settings
        var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
        settingsService.Load();

        // Initialize database (await to ensure tables exist before any writes)
        var db = _serviceProvider.GetRequiredService<DatabaseService>();
        try
        {
            await db.InitializeAsync();
        }
        catch { /* DB init failed — usage tracking will be unavailable */ }

        // Apply saved theme
        var themeService = _serviceProvider.GetRequiredService<ThemeService>();
        themeService.ApplyTheme(settingsService.Settings.SelectedTheme);

        // Start network monitoring
        var monitor = _serviceProvider.GetRequiredService<INetworkMonitorService>();
        monitor.StartMonitoring();

        // Subscribe to stats for usage history tracking
        monitor.StatsUpdated += OnStatsUpdatedForUsage;

        // Start a monitoring session
        try
        {
            var adapterName = monitor.LatestStats?.AdapterName ?? "Unknown";
            _currentSessionId = await db.StartSessionAsync(adapterName);
            await db.LogEventAsync("APP_START", "NetMonitor Pro started", "INFO");
        }
        catch { /* Session start failed — non-critical */ }

        // Start alert service (eagerly to begin monitoring)
        _serviceProvider.GetRequiredService<AlertService>();

        // Take initial process snapshot (so first delta will be meaningful)
        try
        {
            var tracker = _serviceProvider.GetRequiredService<ProcessNetworkTracker>();
            tracker.GetProcessUsageDelta(); // seed the baseline
        }
        catch { /* Process tracking init failed — non-critical */ }

        // Show overlay window
        _overlayWindow = _serviceProvider.GetRequiredService<OverlayWindow>();
        SetWindowIcon(_overlayWindow);
        _overlayWindow.Show();

        // Setup system tray icon
        SetupTrayIcon();

        // Start usage flush timer (every 30 seconds for better granularity)
        _usageFlushTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(30)
        };
        _usageFlushTimer.Tick += OnUsageFlushTick;
        _usageFlushTimer.Start();
    }

    private void OnStatsUpdatedForUsage(object? sender, NetworkStats stats)
    {
        lock (_usageLock)
        {
            // FIX: Multiply speed by the actual interval duration to get real bytes
            var settings = _serviceProvider?.GetService<ISettingsService>();
            var intervalSec = (settings?.Settings.UpdateIntervalMs ?? 1000) / 1000.0;

            _accumulatedDown += (long)(stats.DownloadBytesPerSecond * intervalSec);
            _accumulatedUp += (long)(stats.UploadBytesPerSecond * intervalSec);
            _peakDown = Math.Max(_peakDown, stats.DownloadBytesPerSecond);
            _peakUp = Math.Max(_peakUp, stats.UploadBytesPerSecond);

            // Track session totals
            _sessionTotalDown += (long)(stats.DownloadBytesPerSecond * intervalSec);
            _sessionTotalUp += (long)(stats.UploadBytesPerSecond * intervalSec);
            _sessionTickCount++;
        }
    }

    private async void OnUsageFlushTick(object? sender, EventArgs e)
    {
        long down, up, peakD, peakU;
        lock (_usageLock)
        {
            down = _accumulatedDown;
            up = _accumulatedUp;
            peakD = _peakDown;
            peakU = _peakUp;
            _accumulatedDown = 0;
            _accumulatedUp = 0;
            _peakDown = 0;
            _peakUp = 0;
        }

        if (down == 0 && up == 0) return;

        try
        {
            var db = _serviceProvider?.GetService<DatabaseService>();
            var monitor = _serviceProvider?.GetService<INetworkMonitorService>();
            if (db == null) return;

            var adapterName = monitor?.LatestStats?.AdapterName ?? "Unknown";
            var now = DateTime.Now;
            var today = now.ToString("yyyy-MM-dd");
            var hour = now.Hour;

            // Write to both daily and hourly tables
            await db.UpsertDailyUsageAsync(today, adapterName, down, up, peakD, peakU);
            await db.UpsertHourlyUsageAsync(today, hour, adapterName, down, up, peakD, peakU);
        }
        catch
        {
            // DB write failed — skip this flush
        }

        // Track per-process usage
        try
        {
            var tracker = _serviceProvider?.GetService<ProcessNetworkTracker>();
            var db = _serviceProvider?.GetService<DatabaseService>();
            if (tracker == null || db == null) return;

            var delta = tracker.GetProcessUsageDelta();
            var today = DateTime.Today.ToString("yyyy-MM-dd");

            foreach (var proc in delta)
            {
                if (proc.BytesDownloaded > 1024 || proc.BytesUploaded > 1024)  // Only track if > 1KB
                {
                    await db.UpsertProcessUsageAsync(
                        today, proc.ProcessName, proc.ExecutablePath,
                        proc.BytesDownloaded, proc.BytesUploaded);
                }
            }
        }
        catch
        {
            // Process tracking failed — non-critical
        }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services (singleton — shared state)
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<INetworkMonitorService, NetworkMonitorService>();
        services.AddSingleton<DatabaseService>();
        services.AddSingleton<SpeedTestService>();
        services.AddSingleton<AlertService>();
        services.AddSingleton<ProcessNetworkTracker>();
        services.AddSingleton<ThemeService>();

        // ViewModels (transient — one per window)
        services.AddTransient<OverlayViewModel>();
        services.AddTransient<DashboardViewModel>();

        // Views
        services.AddTransient<OverlayWindow>();
        services.AddTransient<DashboardWindow>();
    }

    private void SetupTrayIcon()
    {
        _trayIcon = new TaskbarIcon
        {
            ToolTipText = "NetMonitor Pro — Click to show/hide",
            ContextMenu = CreateTrayContextMenu(),
        };

        // Load tray icon from embedded PNG resource
        try
        {
            var resourceInfo = GetResourceStream(new Uri("pack://application:,,,/Assets/Nsm tray logo.png"));
            if (resourceInfo != null)
            {
                using var pngStream = resourceInfo.Stream;
                using var bitmap = new System.Drawing.Bitmap(pngStream);
                using var resized = new System.Drawing.Bitmap(bitmap, new System.Drawing.Size(32, 32));
                var hIcon = resized.GetHicon();
                _trayIcon.Icon = System.Drawing.Icon.FromHandle(hIcon);
            }
        }
        catch
        {
            // Fallback: use a generated text icon so tray is always visible
        }

        // If custom icon failed, use generated icon
        if (_trayIcon.Icon == null)
        {
            _trayIcon.IconSource = new GeneratedIconSource
            {
                Text = "N",
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E676")),
                Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1A1A2E")),
                FontSize = 18,
                FontWeight = FontWeights.Bold,
            };
        }

        _trayIcon.ForceCreate(false);
        _trayIcon.TrayMouseDoubleClick += (_, _) => ToggleOverlayVisibility();
    }

    private System.Windows.Controls.ContextMenu CreateTrayContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        _showHideMenuItem = new System.Windows.Controls.MenuItem
        {
            Header = "👁  Show / Hide Overlay",
            IsCheckable = true,
            IsChecked = true // Overlay starts visible
        };
        _showHideMenuItem.Click += (_, _) => ToggleOverlayVisibility();
        menu.Items.Add(_showHideMenuItem);

        var dashboard = new System.Windows.Controls.MenuItem { Header = "📊  Open Dashboard" };
        dashboard.Click += (_, _) => OpenDashboard();
        menu.Items.Add(dashboard);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var quit = new System.Windows.Controls.MenuItem { Header = "❌  Quit" };
        quit.Click += (_, _) => ShutdownApp();
        menu.Items.Add(quit);

        return menu;
    }

    public void OpenDashboard()
    {
        if (_dashboardWindow is { IsLoaded: true })
        {
            _dashboardWindow.Activate();
            return;
        }

        _dashboardWindow = _serviceProvider!.GetRequiredService<DashboardWindow>();
        SetWindowIcon(_dashboardWindow);
        _dashboardWindow.Show();
    }

    private static void SetWindowIcon(Window window)
    {
        try
        {
            // Try ICO first (best quality for window title bar and taskbar)
            window.Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/NetMonitorPro.ico"));
        }
        catch
        {
            try
            {
                // Fallback to PNG
                window.Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/Nsm Pro App Logo.png"));
            }
            catch
            {
                // No icon available — window will use default
            }
        }
    }

    private void ToggleOverlayVisibility()
    {
        if (_overlayWindow == null) return;

        if (_overlayWindow.IsVisible)
            _overlayWindow.Hide();
        else
            _overlayWindow.Show();

        // Update tray checkmark to reflect current visibility
        if (_showHideMenuItem != null)
            _showHideMenuItem.IsChecked = _overlayWindow.IsVisible;
    }

    private async void ShutdownApp()
    {
        // Stop usage timer
        _usageFlushTimer?.Stop();

        // Unsubscribe from stats
        var monitor = _serviceProvider?.GetService<INetworkMonitorService>();
        if (monitor != null)
            monitor.StatsUpdated -= OnStatsUpdatedForUsage;

        var settings = _serviceProvider?.GetService<ISettingsService>();
        settings?.Save();

        // End session with accumulated stats
        try
        {
            var db = _serviceProvider?.GetService<DatabaseService>();
            if (db != null && _currentSessionId > 0)
            {
                var avgDown = _sessionTickCount > 0 ? _sessionTotalDown / _sessionTickCount : 0;
                var avgUp = _sessionTickCount > 0 ? _sessionTotalUp / _sessionTickCount : 0;
                await db.EndSessionAsync(_currentSessionId, _sessionTotalDown, _sessionTotalUp, avgDown, avgUp);
                await db.LogEventAsync("APP_STOP", "NetMonitor Pro stopped", "INFO");
            }
        }
        catch { /* Non-critical */ }

        monitor?.Dispose();

        var tracker = _serviceProvider?.GetService<ProcessNetworkTracker>();
        tracker?.Dispose();

        var dbDispose = _serviceProvider?.GetService<DatabaseService>();
        dbDispose?.Dispose();

        _trayIcon?.Dispose();
        _serviceProvider?.Dispose();

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _usageFlushTimer?.Stop();
        _trayIcon?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}

