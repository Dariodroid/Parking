using Microsoft.Extensions.DependencyInjection;
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

        // ── Constructor ─────────────────────────────────────────────────────
        // IServiceProvider NUNCA puede ser null aquí — si llega null
        // es porque App.xaml.cs no registró los servicios.
        public MainWindowViewModel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider
                ?? throw new ArgumentNullException(nameof(serviceProvider),
                   "IServiceProvider es null. Verifica el registro de servicios en App.xaml.cs");

            // Navegar a la pantalla inicial de forma segura
            NavigateCommand = new RelayCommand(async param => await NavigateAsync(param?.ToString()));
            // Carga la vista inicial sin arriesgar NRE
            //_= NavigateAsync("Operaciones");
            //_ = NavigateAsync("Tipos Vehículo");
        }

        private async Task NavigateAsync(string? destination)
        {
            if (string.IsNullOrWhiteSpace(destination)) return;

            switch (destination)
            {
                case "Operaciones":
                    CurrentView = _serviceProvider.GetRequiredService<PlateReaderViewModel>();
                    PageTitle = "Registro de Entrada / Salida";
                    break;

                case "Dashboard":
                    // CurrentView = _serviceProvider.GetRequiredService<DashboardViewModel>();
                    PageTitle = "Dashboard";
                    break;

                case "Reportes":
                    // CurrentView = _serviceProvider.GetRequiredService<ReportsViewModel>();
                    PageTitle = "Reportes";
                    break;

                case "Config":
                    // CurrentView = _serviceProvider.GetRequiredService<ConfigViewModel>();
                    PageTitle = "Configuración";
                    break;

                case "Clientes":
                    // CurrentView = _serviceProvider.GetRequiredService<UsersViewModel>();
                    PageTitle = "Gestión de Clientes";
                    break;

                case "Seguridad":
                    // CurrentView = _serviceProvider.GetRequiredService<AuditViewModel>();
                    PageTitle = "Seguridad";
                    break;

                case "Caja":
                    PageTitle = "Caja";
                    break;

                case "Tipos Vehículo":
                    var vm = _serviceProvider.GetRequiredService<VehicleTypeViewModel>();

                    CurrentView = vm;
                    PageTitle = "Tipos de Vehículos";

                    await vm.InitializeAsync();

                    break;

                default:
                    // Navegación desconocida — no hacer nada en vez de explotar
                    break;
            }
        }
    }
}
