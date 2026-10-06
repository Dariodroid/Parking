using Parking.UI.Windows.DependencyInjection;
using Parking.UI.Windows.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Parking.Application.Interfaces;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.View.Pages;
using Parking.UI.Windows.ViewModels;
using System;
using System.Windows;

namespace Parking.UI.Windows
{
    public partial class App : System.Windows.Application
    {
        /// <summary>Contenedor de servicios compartido por las ventanas de la aplicación.</summary>
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        /// <summary>Carga preferencias locales, registra servicios y abre configuración o login.</summary>
        /// <param name="e">Argumentos de inicio proporcionados por WPF.</param>
        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // El tema y la conexión pertenecen al usuario actual de esta instalación.
            var settingsStore = new ApplicationSettingsStore();
            // El signo se restaura antes de construir cualquier pantalla o exportación.
            var storedCurrencySymbol = settingsStore.Load().CurrencySymbol;
            CurrencyDisplay.SetSymbol(CurrencyDisplay.IsValidSymbol(storedCurrencySymbol) ? storedCurrencySymbol : "$");
            var themeService = new ThemeService(settingsStore);
            themeService.ApplyStored();
            ServiceProvider = AppServiceRegistration.Create(settingsStore, themeService);

            // Los asistentes cierran sus ventanas antes de abrir el login; la aplicación sigue viva entre pasos.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // La licencia se exige antes de configurar SQL o crear el primer administrador.
            try
            {
                var licenses = ServiceProvider.GetRequiredService<LicenseManager>();
                if (!licenses.TryGetLicense(out _, out string licenseError))
                {
                    var dialogs = ServiceProvider.GetRequiredService<IDialogService>();
                    dialogs.ShowWarning("Licencia requerida", licenseError);
                    if (new LicenseActivationWindow(licenses, dialogs).ShowDialog() != true)
                    {
                        Shutdown();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verificando licencia: {ex}");
                ServiceProvider.GetRequiredService<IDialogService>().ShowError("Inicio del sistema",
                    "No se pudo verificar la identidad o licencia de esta instalación. Contacte al proveedor.");
                Shutdown();
                return;
            }

            // La instalación inicial pide el servidor antes de construir el login.
            if (string.IsNullOrWhiteSpace(settingsStore.Load().ConnectionString))
            {
                var settingsVm = ServiceProvider.GetRequiredService<ApplicationSettingsViewModel>();
                if (new ConnectionSetupWindow(settingsVm).ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            // Una base sin usuarios necesita crear el administrador antes del primer acceso.
            // Si SQL no responde o falta la tabla, no se ofrece un alta alternativa que eluda el control.
            using (var scope = ServiceProvider.CreateScope())
            {
                var initialAdmin = scope.ServiceProvider.GetRequiredService<IInitialAdministratorService>();
                bool isRequired;
                try
                {
                    // Esperar de forma asíncrona deja libre el hilo WPF para que SQL termine y se dibuje la ventana.
                    isRequired = await initialAdmin.IsRequiredAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error comprobando usuarios iniciales: {ex}");
                    ServiceProvider.GetRequiredService<IDialogService>().ShowError("Inicio del sistema",
                        "No se pudo verificar la base de datos. Revise la conexión y la tabla de usuarios.");
                    Shutdown();
                    return;
                }

                if (isRequired && new InitialAdministratorWindow(initialAdmin,
                    ServiceProvider.GetRequiredService<IDialogService>()).ShowDialog() != true)
                {
                    Shutdown();
                    return;
                }
            }

            // El login se crea después de que exista una cadena SQL válida.
            var loginWindow = new LoginPage();
            loginWindow.DataContext =
                ServiceProvider.GetRequiredService<LoginViewModel>();
            loginWindow.Show();
            ShutdownMode = ShutdownMode.OnLastWindowClose;
        }

        /// <summary>Libera los servicios compartidos cuando WPF termina la aplicación.</summary>
        /// <param name="e">Datos del cierre proporcionados por WPF.</param>
        protected override void OnExit(ExitEventArgs e)
        {
            if (ServiceProvider is IDisposable disposable)
                disposable.Dispose();

            base.OnExit(e);
        }
    }
}
