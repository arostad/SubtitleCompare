using System.Globalization;
using System.Windows;
using System.Windows.Data;
using SubtitleCompare.Core.Ui;

namespace SubtitleCompare.App;

public sealed class PickTrackHintWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var width = value is double d ? d : 0;
        return PickTrackHintSize.ArrowWidth(width);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        DependencyProperty.UnsetValue;
}
