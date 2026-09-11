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
        public static IServiceProvider ServiceProvider { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var serviceCollection = new ServiceCollection();

            // ====================== 1. ENTITY FRAMEWORK CORE ======================
            serviceCollection.AddDbContext<parking_dbContext>(options =>
            {
                options.UseSqlServer(
                    "Server=LAPTOP-E00ITAMO\\SQLEXPRESS;Database=parking_db;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;",
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
            serviceCollection.AddSingleton<YoloPlateDetector>(sp =>
                new RfdetrPlateDetector(@"C:\users\Dario Castillo\source\repos\Parking\Parking.Infreastructure.ExternalServices\Model\rfdetr_alpr.onnx"));

            serviceCollection.AddSingleton<ICameraService, OpenCvCameraService>();
            serviceCollection.AddSingleton<IPlateService, PlateReaderService>();
            serviceCollection.AddSingleton<IQrService, QrReaderService>();
            serviceCollection.AddSingleton<IParkingStatusNotifier, ParkingStatusNotifier>();
            serviceCollection.AddSingleton<ExcelExportService>();

            // ====================== 4. SERVICIOS DE APLICACIÓN ======================
            serviceCollection.AddSingleton<IEntryService, EntryService>();

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

            // ====================== 6. VENTANAS ======================
            serviceCollection.AddSingleton<MainWindow>();

            // ====================== BUILD ======================
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Mostrar ventana principal
            //var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            //MainWindow = mainWindow;
            //mainWindow.Show();

            var loginWindow = new LoginPage();



            loginWindow.DataContext =
                ServiceProvider.GetRequiredService<LoginViewModel>();



            loginWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (ServiceProvider is IDisposable disposable)
                disposable.Dispose();

            base.OnExit(e);
        }
    }
}
