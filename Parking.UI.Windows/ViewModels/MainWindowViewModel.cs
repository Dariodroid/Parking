using Microsoft.Extensions.DependencyInjection;
using Parking.Domain.Model.Models;
using Parking.Application.Services;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private object? _currentView;
        private string _pageTitle = "Operaciones";
        private string _currentMenuKey = "Operaciones"; // Propiedad para el menú activo
        private readonly IServiceProvider _serviceProvider;
        // La sesión de captura permanece viva mientras se visitan otras secciones.
        private PlateReaderViewModel? _operationViewModel;

        public object? CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public string PageTitle
        {
            get => _pageTitle;
            set => SetProperty(ref _pageTitle, value);
        }

        // Propiedad que rastrea qué botón del menú debe estar iluminado
        public string CurrentMenuKey
        {
            get => _currentMenuKey;
            set => SetProperty(ref _currentMenuKey, value);
        }

        public ICommand NavigateCommand { get; }

        /// <summary>Indica si el usuario puede consultar informes y administrar el sistema.</summary>
        public bool CanManageSystem { get; }

        /// <summary>Libera las cámaras al cerrar la ventana principal.</summary>
        /// <returns>Tarea que finaliza tras detener las capturas activas.</returns>
        public Task ShutdownCamerasAsync() => _operationViewModel?.DeactivateAsync() ?? Task.CompletedTask;

        /// <summary>Prepara la navegación y conserva el controlador de cámaras durante toda la ventana.</summary>
        /// <param name="serviceProvider">Resuelve cada vista y sus servicios al navegar.</param>
        public MainWindowViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider
                ?? throw new ArgumentNullException(nameof(serviceProvider));

            CanManageSystem = MenuAccessPolicy.IsAdministrator(CurrentUser.Role);

            NavigateCommand = new AsyncRelayCommand(param => NavigateAsync(param?.ToString()));

            //_ = NavigateAsync("Operaciones");
        }

        /// <summary>Comprueba los permisos del rol y muestra la sección solicitada.</summary>
        /// <param name="destination">Nombre del menú solicitado; puede ser nulo.</param>
        /// <returns>Tarea que termina cuando se carga la sección seleccionada.</returns>
        private async Task NavigateAsync(string? destination)
        {
            if (!CurrentUser.IsAuthenticated || string.IsNullOrWhiteSpace(destination)
                || !MenuAccessPolicy.CanNavigate(CurrentUser.Role, destination))
                return;

            // Actualizamos la clave del menú inmediatamente para iluminar el botón
            CurrentMenuKey = destination;

            switch (destination)
            {
                case "Operaciones":
                    // Reutilizar el mismo controlador conserva vídeo, selección y reconocimiento.
                    _operationViewModel ??= _serviceProvider.GetRequiredService<PlateReaderViewModel>();
                    CurrentView = _operationViewModel;
                    PageTitle = "Registro de Entrada / Salida";
                    break;

                case "Dashboard":
                    var dashboardVm = _serviceProvider.GetRequiredService<DashboardViewModel>();
                    CurrentView = dashboardVm;
                    PageTitle = "Dashboard";
                    break;

                case "Reportes":
                    PageTitle = "Reportes";
                    break;

                case "Config":
                    var configVm = _serviceProvider.GetRequiredService<ParkingSlotViewModel>();
                    CurrentView = configVm;
                    PageTitle = "Configuraciones";
                    break;

                case "Apariencia y conexión":
                    CurrentView = _serviceProvider.GetRequiredService<ApplicationSettingsViewModel>();
                    PageTitle = "Preferencias del sistema";
                    break;

                case "Seguridad":
                    PageTitle = "Seguridad";
                    break;

                case "Caja":
                    var cashVm = _serviceProvider.GetRequiredService<CashViewModel>();
                    CurrentView = cashVm;
                    PageTitle = "Caja";
                    break;

                case "Centro de control":
                    var controlVm = _serviceProvider.GetRequiredService<OperationsControlViewModel>();
                    CurrentView = controlVm;
                    PageTitle = "Centro de control";
                    await controlVm.LoadAsync();
                    break;

                case "Tipos Vehículo":
                    var vehicleVm = _serviceProvider.GetRequiredService<VehicleTypeViewModel>();
                    CurrentView = vehicleVm;
                    PageTitle = "Tipos de Vehículos";
                    await vehicleVm.InitializeAsync();
                    break;

                case "Clientes":
                    var client = _serviceProvider.GetRequiredService<RegisteredVehicleViewModel>();
                    CurrentView = client;
                    PageTitle = "Clientes";
                    await client.InitializeAsync();
                    break;

                case "Usuarios":
                    var userVm = _serviceProvider.GetRequiredService<UserViewModel>();
                    CurrentView = userVm;
                    PageTitle = "Gestión de Usuarios";
                    await userVm.InitializeAsync();
                    break;

                case "Reporte Operadores":
                    var vm = _serviceProvider.GetRequiredService<OperatorReportViewModel>();
                    CurrentView = vm;
                    await vm.LoadAsync();
                    break;

                case "Reporte Vehiculos":
                    var vh = _serviceProvider.GetRequiredService<VehicleReportViewModel>();
                    CurrentView = vh;
                    PageTitle = "Vehículos";
                    await vh.LoadData();
                    break;

                case "Reporte Rendimiento":
                    var performance = _serviceProvider.GetRequiredService<ParkingPerformanceViewModel>();
                    CurrentView = performance;
                    PageTitle = "Ocupación y recaudación";
                    await performance.LoadAsync();
                    break;

                default:
                    break;
            }
        }
    }
}
