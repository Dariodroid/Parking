using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Parking.Application.EntityService;
using Parking.Application.UseCases;
using Parking.Domain.Model.Abstractions;
using Parking.Infrastructure.DataAccess;
using Parking.Infrastructure.DataAccess.Repository;
using Parking.Infrastructure.ExternalServices;
using Parking.UI.Windows.View;
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
            serviceCollection.AddDbContext<ParkingDbContext>(options =>
            {
                options.UseSqlServer(
                    "Server=LAPTOP-E00ITAMO\\SQLEXPRESS;Database=parking_db;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;",
                    sql => sql.MigrationsAssembly("Parking.Infrastructure.DataAccess")
                );
            });

            // ====================== 2. REPOSITORIOS ======================
            serviceCollection.AddScoped<IParkingSessionRepository, ParkingSessionRepository>();

            // ====================== 3. SERVICIOS EXTERNOS ======================
            serviceCollection.AddSingleton<YoloPlateDetector>(sp =>
                new RfdetrPlateDetector(@"C:\Users\Dario Castillo\source\repos\Parking\Parking.Infreastructure.ExternalServices\Model\rfdetr_alpr.onnx"));

            serviceCollection.AddSingleton<ICameraService, OpenCvCameraService>();
            serviceCollection.AddSingleton<IPlateService, PlateReaderService>();
            serviceCollection.AddSingleton<IQrService, QrReaderService>();

            // ====================== 4. SERVICIOS DE APLICACIÓN ======================
            serviceCollection.AddSingleton<IEntryService, EntryService>();

            // ====================== 5. VIEWMODELS ======================
            serviceCollection.AddTransient<PlateReaderViewModel>();
            serviceCollection.AddTransient<MainWindowViewModel>();

            // ====================== 6. VENTANAS ======================
            serviceCollection.AddSingleton<MainWindow>();

            // ====================== BUILD ======================
            ServiceProvider = serviceCollection.BuildServiceProvider();

            // Mostrar ventana principal
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (ServiceProvider is IDisposable disposable)
                disposable.Dispose();

            base.OnExit(e);
        }
    }
}