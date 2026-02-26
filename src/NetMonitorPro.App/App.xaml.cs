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

    public static ServiceProvider? Services { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
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

        // Initialize database
        var db = _serviceProvider.GetRequiredService<DatabaseService>();
        _ = db.InitializeAsync();

        // Start network monitoring
        var monitor = _serviceProvider.GetRequiredService<INetworkMonitorService>();
        monitor.StartMonitoring();

        // Subscribe to stats for usage history tracking
        monitor.StatsUpdated += OnStatsUpdatedForUsage;

        // Start alert service (eagerly to begin monitoring)
        _serviceProvider.GetRequiredService<AlertService>();

        // Show overlay window
        _overlayWindow = _serviceProvider.GetRequiredService<OverlayWindow>();
        SetWindowIcon(_overlayWindow);
        _overlayWindow.Show();

        // Setup system tray icon
        SetupTrayIcon();

        // Start usage flush timer (every 60 seconds, write accumulated stats to DB)
        _usageFlushTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(60)
        };
        _usageFlushTimer.Tick += OnUsageFlushTick;
        _usageFlushTimer.Start();
    }

    private void OnStatsUpdatedForUsage(object? sender, NetworkStats stats)
    {
        lock (_usageLock)
        {
            _accumulatedDown += stats.DownloadBytesPerSecond;
            _accumulatedUp += stats.UploadBytesPerSecond;
            _peakDown = Math.Max(_peakDown, stats.DownloadBytesPerSecond);
            _peakUp = Math.Max(_peakUp, stats.UploadBytesPerSecond);
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
            var today = DateTime.Today.ToString("yyyy-MM-dd");

            await db.UpsertDailyUsageAsync(today, adapterName, down, up, peakD, peakU);
        }
        catch
        {
            // DB write failed — skip this flush
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
        window.Icon = new BitmapImage(new Uri("pack://application:,,,/Assets/Nsm Pro App Logo.png"));
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

    private void ShutdownApp()
    {
        // Stop usage timer
        _usageFlushTimer?.Stop();

        // Unsubscribe from stats
        var monitor = _serviceProvider?.GetService<INetworkMonitorService>();
        if (monitor != null)
            monitor.StatsUpdated -= OnStatsUpdatedForUsage;

        var settings = _serviceProvider?.GetService<ISettingsService>();
        settings?.Save();

        monitor?.Dispose();

        var db = _serviceProvider?.GetService<DatabaseService>();
        db?.Dispose();

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

