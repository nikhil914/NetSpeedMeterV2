using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using NetMonitorPro.App.ViewModels;
using NetMonitorPro.Core.Interfaces;
using NetMonitorPro.Native.WindowsAPI;

namespace NetMonitorPro.App.Views;

/// <summary>
/// Frameless floating overlay window that displays real-time network speed.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly ISettingsService _settings;
    private DispatcherTimer? _topmostTimer;

    public OverlayWindow(OverlayViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settings = settings;

        Loaded += OnLoaded;
        Closing += OnClosing;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_settings.Settings.OverlayAutoPosition || _settings.Settings.OverlayLeft < 0)
        {
            PositionNearTaskbar();
        }
        else
        {
            Left = _settings.Settings.OverlayLeft;
            Top = _settings.Settings.OverlayTop;
        }

        // Start a timer that periodically re-asserts Topmost via Win32 API
        // so the overlay cannot be hidden behind the taskbar or other windows.
        _topmostTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _topmostTimer.Tick += OnTopmostTimerTick;
        _topmostTimer.Start();
    }

    private void OnTopmostTimerTick(object? sender, EventArgs e)
    {
        if (!IsVisible) return;

        // Only force topmost when the setting is enabled
        if (DataContext is OverlayViewModel vm && vm.IsAlwaysOnTop)
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                TaskbarHelper.ForceTopmost(hwnd);
            }
        }
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Stop the topmost timer
        _topmostTimer?.Stop();
        _topmostTimer = null;

        // Save position
        _settings.Settings.OverlayLeft = Left;
        _settings.Settings.OverlayTop = Top;
        _settings.Settings.OverlayAutoPosition = false;
        _settings.Save();

        // Dispose the ViewModel
        if (DataContext is IDisposable disposable)
            disposable.Dispose();
    }

    /// <summary>
    /// Snaps the overlay window near the taskbar using native API detection.
    /// </summary>
    private void PositionNearTaskbar()
    {
        // Need to get actual rendered size
        UpdateLayout();
        var width = ActualWidth > 0 ? ActualWidth : 250;
        var height = ActualHeight > 0 ? ActualHeight : 40;

        var workArea = (
            Left: SystemParameters.WorkArea.Left,
            Top: SystemParameters.WorkArea.Top,
            Width: SystemParameters.WorkArea.Width,
            Height: SystemParameters.WorkArea.Height
        );

        var (left, top) = TaskbarHelper.GetOverlaySnapPosition(
            width, height, workArea,
            SystemParameters.PrimaryScreenWidth,
            SystemParameters.PrimaryScreenHeight);

        Left = left;
        Top = top;
    }

    /// <summary>
    /// Allow dragging the window by clicking anywhere on it.
    /// </summary>
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
            // After drag, save new position and disable auto-position
            _settings.Settings.OverlayLeft = Left;
            _settings.Settings.OverlayTop = Top;
            _settings.Settings.OverlayAutoPosition = false;
        }
    }

    private void OpenDashboard_Click(object sender, RoutedEventArgs e)
    {
        if (System.Windows.Application.Current is App app)
            app.OpenDashboard();
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        // Open the Dashboard which contains settings
        if (System.Windows.Application.Current is App app)
            app.OpenDashboard();
    }

    private void ToggleOverlay_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }
}

