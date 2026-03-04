using System.Windows;
using NetMonitorPro.App.ViewModels;

namespace NetMonitorPro.App.Views;

public partial class DashboardWindow : Window
{
    public DashboardWindow(DashboardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        Loaded += async (_, _) =>
        {
            await viewModel.LoadUsageDataCommand.ExecuteAsync(null);
        };

        Closing += (_, _) =>
        {
            if (DataContext is IDisposable d) d.Dispose();
        };
    }
}
