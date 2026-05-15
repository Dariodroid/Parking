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

            switch (destination)
            {
                case "Operaciones":

                    var operationVm =
                        _serviceProvider.GetRequiredService<PlateReaderViewModel>();

                    CurrentView = operationVm;
                    PageTitle = "Registro de Entrada / Salida";

                    break;

                case "Dashboard":

                    PageTitle = "Dashboard";

                    break;

                case "Reportes":

                    PageTitle = "Reportes";

                    break;

                case "Config":

                    PageTitle = "Configuración";

                    break;

                case "Seguridad":

                    PageTitle = "Seguridad";

                    break;

                case "Caja":

                    PageTitle = "Caja";

                    break;

                case "Tipos Vehículo":

                    var vehicleVm =
                        _serviceProvider.GetRequiredService<vehicle_typeViewModel>();

                    CurrentView = vehicleVm;
                    PageTitle = "Tipos de Vehículos";

                    await vehicleVm.InitializeAsync();

                    break;

                case "Clientes":

                    var client =
                        _serviceProvider.GetRequiredService<RegisteredVehicleViewModel>();

                    CurrentView = client;
                    PageTitle = "Clientes";

                    await client.InitializeAsync();

                    break;

                case "Usuarios":

                    var userVm =
                        _serviceProvider.GetRequiredService<userViewModel>();

                    CurrentView = userVm;
                    PageTitle = "Gestión de Usuarios";

                    await userVm.InitializeAsync();

                    break;

                default:
                    break;
            }
        }
    }
}