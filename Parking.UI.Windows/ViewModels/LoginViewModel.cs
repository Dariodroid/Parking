using Parking.Application.Dto;
using Parking.Application.Services;
using Parking.Domain.Model.Abstractions;
using Parking.UI.Windows;
using Parking.UI.Windows.View;
using Parking.UI.Windows.View.Pages;
using Parking.UI.Windows.ViewModels.Base;
using System.Windows;
using System.Windows.Input;


namespace Parking.UI.Windows.ViewModels;


public class LoginViewModel : BaseViewModel
{
    private readonly IDialogService _dialogService;
    private readonly IAuthenticationService _authenticationService;

    private readonly IServiceProvider _serviceProvider;


    private string _username = string.Empty;

    private string _password = string.Empty;

    private string _errorMessage = string.Empty;

    private bool _rememberMe;



    public LoginViewModel(
        IAuthenticationService authenticationService,
        IServiceProvider serviceProvider, IDialogService dialogService)
    {
        _dialogService = dialogService;
        _authenticationService = authenticationService;

        _serviceProvider = serviceProvider
            ?? throw new ArgumentNullException(nameof(serviceProvider));



        LoginCommand = new AsyncRelayCommand(
            LoginAsync,
            CanLogin);

    }

    /// <summary>Usuario introducido; al cambiarse, vuelve a evaluar si se puede iniciar sesión.</summary>
    public string Username
    {
        get => _username;

        set
        {
            if (_username == value)
                return;

            _username = value;

            OnPropertyChanged();
            // Activa o desactiva el botón según la validez de las credenciales actuales.
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Contraseña introducida; al cambiarse, actualiza la disponibilidad del comando.</summary>
    public string Password
    {
        get => _password;

        set
        {
            if (_password == value)
                return;

            _password = value;

            OnPropertyChanged();
            // La condición CanLogin se vuelve a consultar tras editar la contraseña.
            CommandManager.InvalidateRequerySuggested();
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;

        set
        {
            if (_errorMessage == value)
                return;

            _errorMessage = value;

            OnPropertyChanged();
        }
    }

    public bool RememberMe
    {
        get => _rememberMe;

        set
        {
            if (_rememberMe == value)
                return;

            _rememberMe = value;

            OnPropertyChanged();
        }
    }

    public ICommand LoginCommand { get; }


    private bool CanLogin(object? parameter)
    {
        return !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
    }

    private async Task LoginAsync(object? parameter)
    {
        ErrorMessage = string.Empty;

        try
        {

            LoginRequest request = new()
            {
                Username = Username,

                Password = Password
            };
            
            LoginResult result = await _authenticationService.LoginAsync(request);

            if (!result.Success)
            {
                // El servicio conserva el mensaje genérico salvo si la contraseña válida pertenece a una cuenta bloqueada.
                _dialogService.ShowError("Inicio de sesión", result.Message);
                return;
            }

            if (result.User != null)
            {
                CurrentUser.Login(result.User);
            }


            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var mainWindowViewModel = new MainWindowViewModel(_serviceProvider);
                MainWindow mainWindow = new MainWindow(mainWindowViewModel);
                mainWindow.Show();
                foreach (Window window in System.Windows.Application.Current.Windows)
                {
                    if (window is LoginPage)
                    {
                        window.Close();

                        break;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            // El detalle interno puede incluir datos SQL o de una cuenta; solo queda en depuración.
            System.Diagnostics.Debug.WriteLine($"Error de inicio de sesión: {ex}");
            _dialogService.ShowError("Error", "No se pudo iniciar sesión. Inténtelo de nuevo más tarde.");
        }
    }
}
