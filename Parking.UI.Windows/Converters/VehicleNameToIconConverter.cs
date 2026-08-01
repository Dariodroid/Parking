using System;
using System.Globalization;
using System.Windows.Data;
using MaterialDesignThemes.Wpf;

namespace Parking.UI.Windows.Converters
{
    public class VehicleNameToIconConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return PackIconKind.Car;

            string name = value.ToString().ToUpperInvariant();

            if (name.Contains("MOTO")) return PackIconKind.Motorbike;
            if (name.Contains("BICICLETA".ToUpper())) return PackIconKind.Bike;
            if (name.Contains("CAMIÓN") || name.Contains("CAMION")) return PackIconKind.Truck;
            if (name.Contains("SUV") || name.Contains("CAMIONETA")) return PackIconKind.CarSuv;
            if (name.Contains("AUTO")) return PackIconKind.Car;
            if (name.Contains("VAN")) return PackIconKind.VanPassenger;
            if (name.Contains("BUS")) return PackIconKind.Bus;
            if (name.Contains("TRICICLO")) return PackIconKind.BikeElectric;

            return PackIconKind.Car; // Ícono por defecto
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}