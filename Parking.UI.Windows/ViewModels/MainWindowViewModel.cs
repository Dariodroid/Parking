using Microsoft.Extensions.DependencyInjection;
using Parking.Domain.Model.Models;
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

        public MainWindowViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider
                ?? throw new ArgumentNullException(nameof(serviceProvider));

            NavigateCommand = new RelayCommand(async param =>
                await NavigateAsync(param?.ToString()));

            //_ = NavigateAsync("Operaciones");
        }

        private async Task NavigateAsync(string? destination)
        {
            if (string.IsNullOrWhiteSpace(destination))
                return;

            // Actualizamos la clave del menú inmediatamente para iluminar el botón
            CurrentMenuKey = destination;

            switch (destination)
            {
                case "Operaciones":
                    var operationVm = _serviceProvider.GetRequiredService<PlateReaderViewModel>();
                    CurrentView = operationVm;
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

                case "Seguridad":
                    PageTitle = "Seguridad";
                    break;

                case "Caja":
                    var cashVm = _serviceProvider.GetRequiredService<CashViewModel>();
                    CurrentView = cashVm;
                    PageTitle = "Caja";
                    break;

                case "Tipos Vehículo":
                    var vehicleVm = _serviceProvider.GetRequiredService<vehicle_typeViewModel>();
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
                    var userVm = _serviceProvider.GetRequiredService<userViewModel>();
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

                default:
                    break;
            }
        }
    }
}