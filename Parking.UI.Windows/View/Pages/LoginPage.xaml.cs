using Parking.UI.Windows.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Parking.UI.Windows.View.Pages
{
    /// <summary>
    /// Lógica de interacción para LoginPage.xaml
    /// </summary>
    public partial class LoginPage : Window
    {
        public LoginPage()
        {
            InitializeComponent();
        }
        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {

            if (DataContext is LoginViewModel vm)
            {
                vm.Password = PasswordBox.Password;
            }

        }
        private void Border_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                this.DragMove();
            }
        }

        private void CloseButton_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            // Usamos el nombre completo para evitar el error de compilación
            System.Windows.Application.Current.Shutdown();
        }

        private void EyeButton_Click(object sender, RoutedEventArgs e)
        {
            if (RevealPasswordBox.Visibility == Visibility.Visible)
            {
                // Ocultar texto
                RevealPasswordBox.Visibility = Visibility.Collapsed;
                PasswordBox.Visibility = Visibility.Visible;
                EyeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOutline;
                PasswordBox.Focus();
            }
            else
            {
                // Mostrar texto
                RevealPasswordBox.Text = PasswordBox.Password;
                RevealPasswordBox.Visibility = Visibility.Visible;
                PasswordBox.Visibility = Visibility.Collapsed;
                EyeIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.EyeOffOutline;
                RevealPasswordBox.Focus();
                RevealPasswordBox.CaretIndex = RevealPasswordBox.Text.Length;
            }
        }

        private void RevealPasswordBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Si el usuario escribe mientras la contraseña está visible, se actualiza el PasswordBox
            if (RevealPasswordBox.Visibility == Visibility.Visible)
            {
                if (PasswordBox.Password != RevealPasswordBox.Text)
                {
                    PasswordBox.Password = RevealPasswordBox.Text;
                }
            }
        }
    }
}
