using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using NetMonitorPro.App.ViewModels;
using NetMonitorPro.App.Views;
using NetMonitorPro.Core.Interfaces;
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

        // Start alert service (eagerly to begin monitoring)
        _serviceProvider.GetRequiredService<AlertService>();

        // Show overlay window
        _overlayWindow = _serviceProvider.GetRequiredService<OverlayWindow>();
        SetWindowIcon(_overlayWindow);
        _overlayWindow.Show();

        // Setup system tray icon
        SetupTrayIcon();
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

        var showHide = new System.Windows.Controls.MenuItem { Header = "👁  Show / Hide Overlay" };
        showHide.Click += (_, _) => ToggleOverlayVisibility();
        menu.Items.Add(showHide);

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
    }

    private void ShutdownApp()
    {
        var settings = _serviceProvider?.GetService<ISettingsService>();
        settings?.Save();

        var monitor = _serviceProvider?.GetService<INetworkMonitorService>();
        monitor?.Dispose();

        var db = _serviceProvider?.GetService<DatabaseService>();
        db?.Dispose();

        _trayIcon?.Dispose();
        _serviceProvider?.Dispose();

        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trayIcon?.Dispose();
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }
}
