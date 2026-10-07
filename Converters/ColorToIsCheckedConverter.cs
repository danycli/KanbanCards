using System;
using System.Globalization;
using System.Windows.Data;

namespace KanbanCards.Converters;

public class ColorToIsCheckedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() == parameter?.ToString();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool isChecked && isChecked)
        {
            return parameter.ToString()!;
        }
        return Binding.DoNothing;
    }
}
