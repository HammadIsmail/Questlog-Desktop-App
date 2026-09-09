using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Questlog.Common;

/// <summary>
/// Converts bool → Visibility (inverted). False = Visible, True = Collapsed.
/// Used to show UI elements only when a condition is NOT met (e.g., show shift button only on incomplete blocks).
/// </summary>
[ValueConversion(typeof(bool), typeof(Visibility))]
public class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility.Collapsed;
}
