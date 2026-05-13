using Parking.UI.Windows.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Parking.UI.Windows.View.Pages
{
    public partial class UserPage : UserControl
    {
        public UserPage()
        {
            InitializeComponent();
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (PasswordTextBox.Text != PasswordBox.Password)
            {
                PasswordTextBox.Text = PasswordBox.Password;
            }

            if (DataContext is UserViewModel vm)
            {
                vm.Password = PasswordBox.Password;
            }
        }

        private void ConfirmPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (ConfirmPasswordTextBox.Text != ConfirmPasswordBox.Password)
            {
                ConfirmPasswordTextBox.Text = ConfirmPasswordBox.Password;
            }

            if (DataContext is UserViewModel vm)
            {
                vm.ConfirmPassword = ConfirmPasswordBox.Password;
            }
        }

        private void PasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PasswordBox.Password != PasswordTextBox.Text)
            {
                PasswordBox.Password = PasswordTextBox.Text;
            }

            if (DataContext is UserViewModel vm)
            {
                vm.Password = PasswordTextBox.Text;
            }
        }

        private void ConfirmPasswordTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ConfirmPasswordBox.Password != ConfirmPasswordTextBox.Text)
            {
                ConfirmPasswordBox.Password = ConfirmPasswordTextBox.Text;
            }

            if (DataContext is UserViewModel vm)
            {
                vm.ConfirmPassword = ConfirmPasswordTextBox.Text;
            }
        }
    }
}