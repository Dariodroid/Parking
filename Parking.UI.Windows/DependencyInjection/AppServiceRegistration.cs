using Parking.UI.Windows.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Parking.Application.Interfaces;
using Parking.Application.Services;
using Parking.Domain.Model.Interfaces;
using Parking.Domain.Model.Models;
using Parking.Infrastructure.CrossCutting.Security;
using Parking.Infrastructure.CrossCutting.Licensing;
using Parking.Infrastructure.DataAccess;
using Parking.Infrastructure.DataAccess.Repository;
using Parking.Infrastructure.DataAccess.Services;
using Parking.Infrastructure.ExternalServices;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.View;
using Parking.UI.Windows.ViewModels;
using System;

namespace Parking.UI.Windows.DependencyInjection;

/// <summary>Registra las implementaciones y sus tiempos de vida para la aplicación Windows.</summary>
internal static class AppServiceRegistration
{
    /// <summary>Crea el contenedor que conecta Application con SQL, cámaras y vistas.</summary>
    internal static IServiceProvider Create(ApplicationSettingsStore settingsStore, ThemeService themeService)
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton(settingsStore);
        serviceCollection.AddSingleton(themeService);
        serviceCollection.AddSingleton<InstallationIdentity>();
        serviceCollection.AddSingleton<LicenseVerifier>();
        serviceCollection.AddSingleton<LicenseFileStore>();
        serviceCollection.AddSingleton<LicenseManager>();

        // Entity Framework crea un contexto por operación cuando el servicio lo requiere.
        // La fábrica crea un contexto aislado para cada cobro mensual; también
        // permite inyectar el contexto scoped en los repositorios existentes.
        serviceCollection.AddDbContextFactory<parking_dbContext>((provider, options) =>
        {
            // No se distribuye la dirección SQL de la computadora de desarrollo.
            string connectionString = provider.GetRequiredService<ApplicationSettingsStore>()
                .Load().ConnectionString;
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Configure la conexión SQL antes de iniciar sesión.");
            // La estructura SQL se prepara fuera del arranque de la aplicación.
            options.UseSqlServer(connectionString);
        });

        // Los repositorios encapsulan las consultas y escrituras de la base.
        serviceCollection.AddScoped<IParkingSessionRepository, ParkingSessionRepository>();
        serviceCollection.AddScoped<IVehicleTypeRepository, VehicleTypeRepository>();
        serviceCollection.AddScoped<IUserRepository, UserRepository>();
        serviceCollection.AddScoped<IBaseRepository<vehicle_type>, BaseRepository<vehicle_type>>();
        serviceCollection.AddScoped<IBaseRepository<payment>, BaseRepository<payment>>();
        serviceCollection.AddScoped<IRegisteredVehicle, RegisteredVehicleRepository>();
        serviceCollection.AddScoped<IParkingSlotRepository, ParkingSlotRepository>();
        serviceCollection.AddSingleton<IParkingDashboard, ParkingDashboardRepository>();
        serviceCollection.AddScoped<ICashRepository, CashRepository>();
        serviceCollection.AddScoped<IOperatorReportRepository, OperatorReportRepository>();
        serviceCollection.AddScoped<IParkingPerformanceRepository, ParkingPerformanceRepository>();
        serviceCollection.AddScoped<IVehicleReportRepository, VehicleReportRepository>();
        serviceCollection.AddSingleton<IMonthlyFeeLedgerRepository, MonthlyFeeLedgerRepository>();
        serviceCollection.AddTransient<IControlIncidentRepository, ControlIncidentRepository>();
        serviceCollection.AddTransient<IShiftClosingRepository, ShiftClosingRepository>();

        // Adaptadores para cámaras, archivos, impresión y servicios técnicos.
        // Los visores reciben capturas independientes y el catálogo enumera las cámaras Windows.
        serviceCollection.AddSingleton<ICameraServiceFactory, OpenCvCameraServiceFactory>();
        serviceCollection.AddSingleton<ICameraSourceCatalog, OpenCvCameraSourceCatalog>();
        // Las selecciones y URL RTSP se conservan cifradas para la cuenta de Windows.
        serviceCollection.AddSingleton<CameraSelectionStore>();
        serviceCollection.AddSingleton<CameraHealthMonitor>();
        // El libro mensual comparte la base SQL y conserva un asiento por cuota cobrada.
        serviceCollection.AddSingleton<IConnectionStringProvider, ApplicationConnectionStringProvider>();
        serviceCollection.AddSingleton<ICurrencyFormatter, ApplicationCurrencyFormatter>();
        serviceCollection.AddSingleton<ISqlConnectionTester, SqlConnectionTester>();
        serviceCollection.AddScoped<IPasswordHasher, PasswordHasher>();
        serviceCollection.AddScoped<IInitialAdministratorService, InitialAdministratorService>();
        serviceCollection.AddSingleton<IPlateService, PlateReaderService>();
        serviceCollection.AddSingleton<IFrameOverlayRenderer, OpenCvFrameOverlayRenderer>();
        serviceCollection.AddSingleton<IEntryPhotoStore, LocalEntryPhotoStore>();
        serviceCollection.AddSingleton<IQrService, QrReaderService>();
        serviceCollection.AddSingleton<IQrTicketStore, LocalQrTicketStore>();
        serviceCollection.AddSingleton<IParkingStatusNotifier, ParkingStatusNotifier>();
        serviceCollection.AddSingleton<ExcelExportService>();
        // La cola térmica se consulta en Windows y el ticket sale tras confirmar SQL.
        serviceCollection.AddSingleton<ThermalTicketPrinter>();
        serviceCollection.AddTransient<EntryTicketPrintService>();
        serviceCollection.AddTransient<CameraPreviewService>();

        // Application coordina las operaciones que solicitan los ViewModels.
        // EntryService usa repositorios DbContext scoped: comparte una sola
        // unidad de trabajo al registrar sesión y ocupación del puesto.
        serviceCollection.AddScoped<IEntryService, EntryService>();
        serviceCollection.AddScoped<IExitService, ExitService>();
        serviceCollection.AddScoped<ICashQueryService, CashQueryService>();
        serviceCollection.AddScoped<IReportQueryService, ReportQueryService>();
        serviceCollection.AddSingleton<IParkingDashboardService, ParkingDashboardService>();
        serviceCollection.AddScoped<IAuthenticationService, AuthenticationService>();
        serviceCollection.AddScoped<IUserManagementService, UserManagementService>();
        serviceCollection.AddScoped<IVehicleTypeManagementService, VehicleTypeManagementService>();
        serviceCollection.AddScoped<IParkingSlotManagementService, ParkingSlotManagementService>();
        serviceCollection.AddScoped<IRegisteredVehicleManagementService, RegisteredVehicleManagementService>();
        serviceCollection.AddSingleton<IMonthlyFeeLedgerService, MonthlyFeeLedgerService>();
        serviceCollection.AddTransient<IControlIncidentService, ControlIncidentService>();
        serviceCollection.AddTransient<IShiftClosingService, ShiftClosingService>();
        serviceCollection.AddSingleton<IDialogService, DialogService>();

        // Cada pantalla recibe sus dependencias a través del constructor.
        serviceCollection.AddTransient<MainWindowViewModel>();
        serviceCollection.AddTransient<PlateReaderViewModel>();
        serviceCollection.AddTransient<VehicleTypeViewModel>();
        serviceCollection.AddTransient<UserViewModel>();
        serviceCollection.AddTransient<RegisteredVehicleViewModel>();
        serviceCollection.AddTransient<ParkingSlotViewModel>();
        serviceCollection.AddSingleton<DashboardViewModel>();
        serviceCollection.AddTransient<CashViewModel>();
        serviceCollection.AddTransient<OperatorReportViewModel>();
        serviceCollection.AddTransient<ParkingPerformanceViewModel>();
        serviceCollection.AddTransient<OperationsControlViewModel>();
        serviceCollection.AddTransient<VehicleReportViewModel>();
        serviceCollection.AddTransient<LoginViewModel>();
        serviceCollection.AddTransient<ApplicationSettingsViewModel>();

        serviceCollection.AddSingleton<MainWindow>();

        return serviceCollection.BuildServiceProvider();
    }
}
