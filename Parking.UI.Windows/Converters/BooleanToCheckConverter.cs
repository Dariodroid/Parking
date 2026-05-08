using System;
using System.Globalization;
using System.Windows.Data;

namespace Parking.UI.Windows.Converters
{
    public class BooleanToCheckConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
                return isActive ? "✔" : "✖";

            return "✖";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string text)
                return text == "✔";

            return false;
        }
    }
}