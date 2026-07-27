using System.Globalization;
using System.Windows;
using System.Windows.Data;
using MertClicker.Domain;

namespace MertClicker.App.Converters;

public sealed class RepeatModeToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is RepeatMode.FixedCount ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
