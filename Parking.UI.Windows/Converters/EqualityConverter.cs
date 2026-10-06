using System;
using System.Globalization;
using System.Windows.Data;

namespace Parking.UI.Windows.Converters
{
    /// <summary>Compara los dos valores de un enlace múltiple.</summary>
    public class EqualityConverter : IMultiValueConverter
    {
        /// <summary>Indica si ambos valores representan el mismo elemento seleccionado.</summary>
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2)
                return false;

            // Compara el ítem del botón con el SelectedVehicleType del ViewModel
            return Equals(values[0], values[1]);
        }

        /// <summary>La comparación no modifica ninguno de los valores originales.</summary>
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            var results = new object[targetTypes.Length];
            for (int index = 0; index < results.Length; index++)
                results[index] = Binding.DoNothing;
            return results;
        }
    }
}
