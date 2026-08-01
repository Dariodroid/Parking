using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Parking.UI.Windows.Converters
{
    // 1. Evalúa el menú principal y los desplegables para decidir el ancho
    public class MenuWidthConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = MenuToggle.IsChecked
            // values[1] = ExpanderReportes.IsExpanded
            // values[2] = ExpanderConfig.IsExpanded

            bool menuExpanded = values.Length > 0 && values[0] is bool b1 && b1;
            bool exp1Expanded = values.Length > 1 && values[1] is bool b2 && b2;
            bool exp2Expanded = values.Length > 2 && values[2] is bool b3 && b3;

            if (menuExpanded) return new GridLength(240); // Ancho completo
            if (exp1Expanded || exp2Expanded) return new GridLength(240); // Ancho medio si hay un desplegable abierto
            return new GridLength(100); // Ancho contraído
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    // 2. Oculta los textos si el ancho es 100, pero los muestra si es 150 o 240
    public class DoubleToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d && d > 105) // Si el ancho es mayor a 105, muestra el texto
                return Visibility.Visible;
            return Visibility.Collapsed; // Si no, lo oculta
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}