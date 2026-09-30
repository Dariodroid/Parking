using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Parking.Application.Dto;
using Parking.Application.Dto.Interfaces;
using Parking.Application.EntityService;
using Parking.Application.Services;
using Parking.Application.UseCases;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.Infrastructure.CrossCutting.Security;
using Parking.Infrastructure.DataAccess;
using Parking.Infrastructure.DataAccess.Repository;
using Parking.Infrastructure.ExternalServices;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.View;
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
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(settingsStore);
            serviceCollection.AddSingleton(themeService);

            // ====================== 1. ENTITY FRAMEWORK CORE ======================
            serviceCollection.AddDbContext<parking_dbContext>((provider, options) =>
            {
                // No se distribuye la dirección SQL de la computadora de desarrollo.
                string connectionString = provider.GetRequiredService<ApplicationSettingsStore>()
                    .Load().ConnectionString;
                if (string.IsNullOrWhiteSpace(connectionString))
                    throw new InvalidOperationException("Configure la conexión SQL antes de iniciar sesión.");
                // El contexto principal se obtuvo por ingeniería inversa; no hay migraciones EF.
                options.UseSqlServer(connectionString);
            });

            // ====================== 2. REPOSITORIOS ======================
            serviceCollection.AddScoped<Iparking_sessionRepository, parking_sessionRepository>();
            serviceCollection.AddScoped<Ivehicle_typeRepository, vehicle_typeRepository>();
            serviceCollection.AddScoped<IuserRepository, userRepository>();
            serviceCollection.AddScoped<IBaseRepository<vehicle_type>, BaseRepository<vehicle_type>>();
            serviceCollection.AddScoped<IBaseRepository<payment>, BaseRepository<payment>>();
            serviceCollection.AddScoped<IRegisteredVehicle, RegisteredVehicleRepository>();
            serviceCollection.AddScoped<IParkingSlotRepository, ParkingSlotRepository>();
            serviceCollection.AddScoped<IParkingDashboard, ParkingDashboardRepository>();
            serviceCollection.AddScoped<ICashRepository, CashRepository>();
            serviceCollection.AddScoped<IOperatorReportRepository,OperatorReportRepository>();
            serviceCollection.AddScoped<IParkingPerformanceRepository, ParkingPerformanceRepository>();
            serviceCollection.AddScoped<Application.Dto.Interfaces.IVehicleReportRepository,VehicleReportRepository>();
            serviceCollection.AddScoped<IPasswordHasher,PasswordHasher>();

            serviceCollection.AddScoped<IAuthenticationService, AuthenticationService>();
            serviceCollection.AddScoped<IPasswordHasher, PasswordHasher>();
            // La primera cuenta se crea solo tras comprobar la tabla users de la base configurada.
            serviceCollection.AddScoped<InitialAdministratorService>();
            // ====================== 3. SERVICIOS EXTERNOS ======================
            serviceCollection.AddSingleton<YoloPlateDetector>(_ =>
                new RfdetrPlateDetector(System.IO.Path.Combine(
                    AppContext.BaseDirectory, "Model", "rfdetr_alpr.onnx")));

            // Los visores reciben capturas independientes y el catálogo enumera las cámaras Windows.
            serviceCollection.AddSingleton<ICameraServiceFactory, OpenCvCameraServiceFactory>();
            serviceCollection.AddSingleton<ICameraSourceCatalog, OpenCvCameraSourceCatalog>();
            // Las selecciones y URL RTSP se conservan cifradas para la cuenta de Windows.
            serviceCollection.AddSingleton<CameraSelectionStore>();
            serviceCollection.AddSingleton<CameraHealthMonitor>();
            // El libro mensual comparte la base SQL y conserva un asiento por cuota cobrada.
            serviceCollection.AddSingleton<MonthlyFeeLedgerService>();
            serviceCollection.AddTransient<OperationsControlService>();
            serviceCollection.AddSingleton<IPlateService, PlateReaderService>();
            serviceCollection.AddSingleton<IEntryPhotoStore, LocalEntryPhotoStore>();
            serviceCollection.AddSingleton<IQrService, QrReaderService>();
            serviceCollection.AddSingleton<IQrTicketStore, LocalQrTicketStore>();
            serviceCollection.AddSingleton<IParkingStatusNotifier, ParkingStatusNotifier>();
            serviceCollection.AddSingleton<ExcelExportService>();
            // La cola térmica se consulta en Windows y el ticket sale tras confirmar SQL.
            serviceCollection.AddSingleton<ThermalTicketPrinter>();

            // ====================== 4. SERVICIOS DE APLICACIÓN ======================
            // EntryService usa repositorios DbContext scoped: comparte una sola
            // unidad de trabajo al registrar sesión y ocupación del puesto.
            serviceCollection.AddScoped<IEntryService, EntryService>();
            // En tu App.xaml.cs o donde configures la inyección
            serviceCollection.AddSingleton<IDialogService, DialogService>();

            // ====================== 5. VIEWMODELS ======================
            serviceCollection.AddTransient<MainWindowViewModel>();
            serviceCollection.AddTransient<PlateReaderViewModel>();
            serviceCollection.AddTransient<vehicle_typeViewModel>();
            serviceCollection.AddTransient<userViewModel>();
            serviceCollection.AddTransient<RegisteredVehicleViewModel>();
            serviceCollection.AddTransient<ParkingSlotViewModel>();
            serviceCollection.AddSingleton<DashboardViewModel>();
            serviceCollection.AddTransient<CashViewModel>();
            serviceCollection.AddTransient<OperatorReportViewModel>();
            serviceCollection.AddTransient<ParkingPerformanceViewModel>();
            serviceCollection.AddTransient<OperationsControlViewModel>();
            serviceCollection.AddTransient<VehicleReportViewModel>();
            serviceCollection.AddTransient<VehicleReportViewModel>();
            serviceCollection.AddTransient<LoginViewModel>();
            serviceCollection.AddTransient<ApplicationSettingsViewModel>();

            // ====================== 6. VENTANAS ======================
            serviceCollection.AddSingleton<MainWindow>();

            // ====================== BUILD ======================
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Los asistentes cierran sus ventanas antes de abrir el login; la aplicación sigue viva entre pasos.
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

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
                var initialAdmin = scope.ServiceProvider.GetRequiredService<InitialAdministratorService>();
                bool isRequired;
                try
                {
                    // Esperar de forma asíncrona deja libre el hilo WPF para que SQL termine y se dibuje la ventana.
                    isRequired = await initialAdmin.IsRequiredAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error comprobando usuarios iniciales: {ex}");
                    MessageBox.Show("No se pudo verificar la base de datos. Revise la conexión y la tabla de usuarios.",
                        "Inicio del sistema", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }

                if (isRequired && new InitialAdministratorWindow(initialAdmin).ShowDialog() != true)
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
