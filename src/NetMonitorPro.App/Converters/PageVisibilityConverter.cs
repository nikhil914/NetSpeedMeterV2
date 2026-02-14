using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace NetMonitorPro.App.Converters;

/// <summary>
/// Converts the current page name to Visibility. 
/// Returns Visible if the bound value matches the ConverterParameter, Collapsed otherwise.
/// Used for navigation in the dashboard.
/// </summary>
public class PageVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is string currentPage && parameter is string targetPage)
            return currentPage == targetPage ? Visibility.Visible : Visibility.Collapsed;
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
