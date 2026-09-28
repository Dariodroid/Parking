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
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // El tema y la conexión pertenecen al usuario actual de esta instalación.
            var settingsStore = new ApplicationSettingsStore();
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
                options.UseSqlServer(
                    connectionString,
                    sql => sql.MigrationsAssembly("Parking.Infrastructure.DataAccess")
                );
            });

            // ====================== 2. REPOSITORIOS ======================
            serviceCollection.AddScoped<Iparking_sessionRepository, parking_sessionRepository>();
            serviceCollection.AddScoped<Ivehicle_typeRepository, vehicle_typeRepository>();
            serviceCollection.AddScoped<IuserRepository, userRepository>();
            serviceCollection.AddScoped<IBaseRepository<vehicle_type>, BaseRepository<vehicle_type>>();
            serviceCollection.AddScoped<IRegisteredVehicle, RegisteredVehicleRepository>();
            serviceCollection.AddScoped<IParkingSlotRepository, ParkingSlotRepository>();
            serviceCollection.AddScoped<IParkingDashboard, ParkingDashboardRepository>();
            serviceCollection.AddScoped<ICashRepository, CashRepository>();
            serviceCollection.AddScoped<IOperatorReportRepository,OperatorReportRepository>();
            serviceCollection.AddScoped<Application.Dto.Interfaces.IVehicleReportRepository,VehicleReportRepository>();
            serviceCollection.AddScoped<IPasswordHasher,PasswordHasher>();

            serviceCollection.AddScoped<IAuthenticationService, AuthenticationService>();
            serviceCollection.AddScoped<IPasswordHasher, PasswordHasher>();
            // ====================== 3. SERVICIOS EXTERNOS ======================
            serviceCollection.AddSingleton<YoloPlateDetector>(_ =>
                new RfdetrPlateDetector(System.IO.Path.Combine(
                    AppContext.BaseDirectory, "Model", "rfdetr_alpr.onnx")));

            // Los visores reciben capturas independientes y el catálogo enumera las cámaras Windows.
            serviceCollection.AddSingleton<ICameraServiceFactory, OpenCvCameraServiceFactory>();
            serviceCollection.AddSingleton<ICameraSourceCatalog, OpenCvCameraSourceCatalog>();
            // Las selecciones y URL RTSP se conservan cifradas para la cuenta de Windows.
            serviceCollection.AddSingleton<CameraSelectionStore>();
            serviceCollection.AddSingleton<IPlateService, PlateReaderService>();
            serviceCollection.AddSingleton<IEntryPhotoStore, LocalEntryPhotoStore>();
            serviceCollection.AddSingleton<IQrService, QrReaderService>();
            serviceCollection.AddSingleton<IQrTicketStore, LocalQrTicketStore>();
            serviceCollection.AddSingleton<IParkingStatusNotifier, ParkingStatusNotifier>();
            serviceCollection.AddSingleton<ExcelExportService>();

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
            serviceCollection.AddTransient<VehicleReportViewModel>();
            serviceCollection.AddTransient<VehicleReportViewModel>();
            serviceCollection.AddTransient<LoginViewModel>();
            serviceCollection.AddTransient<ApplicationSettingsViewModel>();

            // ====================== 6. VENTANAS ======================
            serviceCollection.AddSingleton<MainWindow>();

            // ====================== BUILD ======================
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // La instalación inicial pide el servidor antes de construir el login.
            if (string.IsNullOrWhiteSpace(settingsStore.Load().ConnectionString))
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var settingsVm = ServiceProvider.GetRequiredService<ApplicationSettingsViewModel>();
                if (new ConnectionSetupWindow(settingsVm).ShowDialog() != true)
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

        protected override void OnExit(ExitEventArgs e)
        {
            if (ServiceProvider is IDisposable disposable)
                disposable.Dispose();

            base.OnExit(e);
        }
    }
}
