using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace Parking.UI.Windows.Converters
{
    /// <summary>Elige un icono para el nombre visible de un tipo de vehículo.</summary>
    public class VehicleNameToIconConverter : IValueConverter
    {
        /// <summary>Busca una categoría conocida y usa el automóvil como valor predeterminado.</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string name = value?.ToString()?.ToUpperInvariant() ?? string.Empty;

            if (name.Contains("MOTO")) return PackIconKind.Motorbike;
            if (name.Contains("BICICLETA")) return PackIconKind.Bike;
            if (name.Contains("CAMIÓN") || name.Contains("CAMION")) return PackIconKind.Truck;
            if (name.Contains("SUV") || name.Contains("CAMIONETA")) return PackIconKind.CarSuv;
            if (name.Contains("AUTO")) return PackIconKind.Car;
            if (name.Contains("VAN")) return PackIconKind.VanPassenger;
            if (name.Contains("BUS")) return PackIconKind.Bus;
            if (name.Contains("TRICICLO")) return PackIconKind.BikeElectric;

            return PackIconKind.Car;
        }

        /// <summary>El icono no cambia el nombre original del tipo de vehículo.</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return Binding.DoNothing;
        }
    }
}
