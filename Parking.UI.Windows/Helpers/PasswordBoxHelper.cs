using System.Windows;
using System.Windows.Controls;

namespace Parking.UI.Windows.Helpers
{
    public static class PasswordBoxHelper
    {
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached("BoundPassword", typeof(string), typeof(PasswordBoxHelper),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

        public static string GetBoundPassword(DependencyObject d) => (string)d.GetValue(BoundPasswordProperty);
        public static void SetBoundPassword(DependencyObject d, string value) => d.SetValue(BoundPasswordProperty, value);

        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox passwordBox)
            {
                passwordBox.PasswordChanged -= PasswordBox_PasswordChanged;

                if (!string.Equals(passwordBox.Password, e.NewValue as string))
                {
                    passwordBox.Password = e.NewValue as string ?? string.Empty;
                }

                passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
            }
        }

        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                // BLINDAJE CONTRA WPF:
                // Si WPF intenta vaciar el PasswordBox ("") justamente cuando se está ocultando (Collapsed),
                // ignoramos el evento para proteger la contraseña del ViewModel.
                if (passwordBox.Visibility != Visibility.Visible && string.IsNullOrEmpty(passwordBox.Password))
                {
                    return;
                }

                passwordBox.SetValue(
    BoundPasswordProperty,
    passwordBox.Password);
            }
        }
    }
}