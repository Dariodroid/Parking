using Microsoft.Data.SqlClient;
using Parking.Application.Services;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Diagnostics;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

/// <summary>Permite escoger el tema y configurar la base SQL de este equipo.</summary>
public sealed class ApplicationSettingsViewModel : BaseViewModel
{
    private readonly ApplicationSettingsStore _store;
    private readonly ThemeService _themeService;
    private readonly ThermalTicketPrinter _ticketPrinter;
    private string _connectionString;
    private string _currencySymbol;
    private string _status = string.Empty;
    private bool _isBusy;
    private string _selectedTicketPrinter = string.Empty;
    private int _ticketPaperWidthMm = 80;
    private bool _autoPrintTickets;

    /// <summary>Impresoras visibles para el usuario actual de Windows.</summary>
    public ObservableCollection<string> TicketPrinters { get; } = new();

    /// <summary>Anchos de papel admitidos por el diseño del ticket.</summary>
    public IReadOnlyList<int> TicketPaperWidths { get; } = new[] { 58, 80 };

    /// <summary>Cola seleccionada para entradas ocasionales.</summary>
    public string SelectedTicketPrinter
    {
        get => _selectedTicketPrinter;
        set => SetProperty(ref _selectedTicketPrinter, value);
    }

    /// <summary>Ancho de rollo configurado en el controlador térmico.</summary>
    public int TicketPaperWidthMm
    {
        get => _ticketPaperWidthMm;
        set => SetProperty(ref _ticketPaperWidthMm, value);
    }

    /// <summary>Activa la impresión automática tras confirmar la entrada.</summary>
    public bool AutoPrintTickets
    {
        get => _autoPrintTickets;
        set => SetProperty(ref _autoPrintTickets, value);
    }

    /// <summary>Solo el administrador cambia la impresora usada en este equipo.</summary>
    public bool CanManagePrinter => CanManageCurrency;

    /// <summary>Permite editar SQL solo al administrador autenticado o durante la primera instalación.</summary>
    public bool CanManageConnection =>
        (CurrentUser.IsAuthenticated
            && string.Equals(CurrentUser.Role, "Administrador", StringComparison.OrdinalIgnoreCase))
        || (!CurrentUser.IsAuthenticated
            && string.IsNullOrWhiteSpace(_store.Load().ConnectionString));

    /// <summary>Solo el administrador autenticado puede cambiar el signo visible del dinero.</summary>
    public bool CanManageCurrency => CurrentUser.IsAuthenticated
        && string.Equals(CurrentUser.Role, "Administrador", StringComparison.OrdinalIgnoreCase);

    /// <summary>Signo visual que se aplicará a la instalación actual.</summary>
    public string CurrencySymbol
    {
        get => _currencySymbol;
        set => SetProperty(ref _currencySymbol, value);
    }

    /// <summary>Ejemplo actualizado tras guardar el signo.</summary>
    public string CurrencyPreview => CurrencyDisplay.Format(1234.56m);

    /// <summary>Se dispara después de guardar una conexión válida.</summary>
    public event EventHandler? ConnectionSaved;

    /// <summary>Cadena SQL editable que se cifra al guardar.</summary>
    public string ConnectionString
    {
        get => _connectionString;
        set => SetProperty(ref _connectionString, value);
    }

    /// <summary>Mensaje de validación o resultado de prueba visible al operador.</summary>
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    /// <summary>Indica que se está comprobando la conexión.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    /// <summary>Opción clara de apariencia.</summary>
    public bool IsLightTheme
    {
        get => _themeService.Current == AppThemeMode.Light;
        set { if (value) ChangeTheme(AppThemeMode.Light); }
    }

    /// <summary>Opción oscura de apariencia.</summary>
    public bool IsDarkTheme
    {
        get => _themeService.Current == AppThemeMode.Dark;
        set { if (value) ChangeTheme(AppThemeMode.Dark); }
    }

    /// <summary>Comprueba acceso de lectura a la base indicada.</summary>
    public ICommand TestConnectionCommand { get; }

    /// <summary>Guarda la conexión para próximas ejecuciones.</summary>
    public ICommand SaveConnectionCommand { get; }

    /// <summary>Reinicia la aplicación después de cambiar de servidor.</summary>
    public ICommand RestartCommand { get; }

    /// <summary>Valida y guarda el signo de moneda sin cambiar importes.</summary>
    public ICommand SaveCurrencyCommand { get; }

    /// <summary>Vuelve a consultar las impresoras instaladas en Windows.</summary>
    public ICommand RefreshTicketPrintersCommand { get; }

    /// <summary>Guarda la impresora y las opciones de ticket para este usuario Windows.</summary>
    public ICommand SaveTicketPrinterCommand { get; }

    /// <summary>Prepara preferencias y comandos sin abrir todavía SQL.</summary>
    /// <param name="store">Almacén cifrado local.</param>
    /// <param name="themeService">Control del tema global.</param>
    /// <param name="ticketPrinter">Consulta las colas instaladas de Windows.</param>
    public ApplicationSettingsViewModel(ApplicationSettingsStore store, ThemeService themeService,
        ThermalTicketPrinter ticketPrinter)
    {
        _store = store;
        _themeService = themeService;
        _ticketPrinter = ticketPrinter;
        _currencySymbol = store.Load().CurrencySymbol;
        var printerSettings = store.Load();
        _selectedTicketPrinter = printerSettings.TicketPrinterName;
        _ticketPaperWidthMm = printerSettings.TicketPaperWidthMm is 58 or 80
            ? printerSettings.TicketPaperWidthMm : 80;
        _autoPrintTickets = printerSettings.AutoPrintTickets;
        // Los operadores no reciben la cadena, que podría contener una contraseña SQL.
        _connectionString = CanManageConnection ? store.Load().ConnectionString : string.Empty;
        TestConnectionCommand = new AsyncRelayCommand(_ => TestConnectionAsync());
        SaveConnectionCommand = new RelayCommand(_ => SaveConnection());
        RestartCommand = new RelayCommand(_ => Restart());
        SaveCurrencyCommand = new RelayCommand(_ => SaveCurrency());
        RefreshTicketPrintersCommand = new RelayCommand(_ => RefreshTicketPrinters(showStatus: true));
        SaveTicketPrinterCommand = new RelayCommand(_ => SaveTicketPrinter());
        if (CanManagePrinter) RefreshTicketPrinters(showStatus: false);
    }

    /// <summary>Actualiza el catálogo local y conserva la selección si sigue instalada.</summary>
    /// <param name="showStatus">Muestra el recuento al solicitar una búsqueda manual.</param>
    private void RefreshTicketPrinters(bool showStatus)
    {
        if (!CanManagePrinter) return;
        try
        {
            // Una impresora desconectada no borra la preferencia hasta que se guarde otra.
            string selected = SelectedTicketPrinter;
            TicketPrinters.Clear();
            foreach (string name in _ticketPrinter.GetPrinterNames()) TicketPrinters.Add(name);
            SelectedTicketPrinter = TicketPrinters.Contains(selected) ? selected : string.Empty;
            if (showStatus)
                Status = TicketPrinters.Count == 0
                    ? "Windows no muestra impresoras instaladas para este usuario."
                    : $"Se encontraron {TicketPrinters.Count} impresoras.";
        }
        catch (Exception ex)
        {
            Status = $"No se pudo consultar las impresoras de Windows: {ex.Message}";
        }
    }

    /// <summary>Persiste la cola, el ancho y la preferencia de impresión automática.</summary>
    private void SaveTicketPrinter()
    {
        if (!CanManagePrinter)
        {
            Status = "Solo un administrador puede configurar la impresora.";
            return;
        }
        if (TicketPaperWidthMm is not (58 or 80))
        {
            Status = "Elija papel de 58 u 80 mm.";
            return;
        }
        if (AutoPrintTickets && !TicketPrinters.Contains(SelectedTicketPrinter))
        {
            Status = "Elija una impresora de Windows antes de activar la impresión automática.";
            return;
        }
        try
        {
            // El ajuste no se guarda dentro del ejecutable ni modifica sesiones SQL.
            _store.Save(_store.Load() with
            {
                TicketPrinterName = SelectedTicketPrinter,
                TicketPaperWidthMm = TicketPaperWidthMm,
                AutoPrintTickets = AutoPrintTickets
            });
            Status = "Configuración de tickets guardada para este usuario de Windows.";
        }
        catch (Exception ex)
        {
            Status = $"No se pudo guardar la impresora: {ex.Message}";
        }
    }

    /// <summary>Guarda el signo para este perfil Windows y actualiza las nuevas vistas y exportaciones.</summary>
    private void SaveCurrency()
    {
        if (!CanManageCurrency)
        {
            Status = "Solo un administrador puede cambiar el signo de moneda.";
            return;
        }
        if (!CurrencyDisplay.IsValidSymbol(CurrencySymbol))
        {
            Status = "Use un signo de hasta ocho caracteres, sin cifras ni separadores decimales.";
            return;
        }
        try
        {
            _store.Save(_store.Load() with { CurrencySymbol = CurrencySymbol });
            CurrencyDisplay.SetSymbol(CurrencySymbol);
            OnPropertyChanged(nameof(CurrencyPreview));
            Status = "Signo guardado. Las próximas vistas e informes usarán este signo; los importes de la base no cambian.";
        }
        catch (Exception ex)
        {
            Status = $"No se pudo guardar el signo: {ex.Message}";
        }
    }

    /// <summary>Aplica y guarda el tema sin alterar la configuración SQL.</summary>
    /// <param name="mode">Nueva apariencia.</param>
    private void ChangeTheme(AppThemeMode mode)
    {
        try
        {
            // El tema visual se actualiza también en ventanas ya abiertas.
            _themeService.Apply(mode);
            OnPropertyChanged(nameof(IsLightTheme));
            OnPropertyChanged(nameof(IsDarkTheme));
        }
        catch (Exception ex)
        {
            Status = $"No se pudo guardar el tema: {ex.Message}";
        }
    }

    /// <summary>Comprueba sintaxis, servidor y nombre de base sin mostrar la contraseña.</summary>
    /// <param name="connectionString">Cadena introducida por el usuario.</param>
    /// <param name="builder">Resultado listo para abrir SQL.</param>
    /// <returns>Verdadero si están presentes los datos mínimos.</returns>
    private static bool TryBuild(string connectionString, out SqlConnectionStringBuilder? builder)
    {
        builder = null;
        try
        {
            var candidate = new SqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(candidate.DataSource)
                || string.IsNullOrWhiteSpace(candidate.InitialCatalog)) return false;
            builder = candidate;
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    /// <summary>Abre y cierra la conexión propuesta sin modificar la base.</summary>
    /// <returns>Tarea que muestra el resultado de conectividad.</returns>
    private async Task TestConnectionAsync()
    {
        if (!CanManageConnection)
        {
            Status = "Solo un administrador puede configurar la conexión.";
            return;
        }

        if (!TryBuild(ConnectionString, out var builder))
        {
            Status = "Indique Server y Database en una cadena SQL válida.";
            return;
        }

        IsBusy = true;
        Status = "Comprobando la conexión...";
        try
        {
            // Un tiempo corto permite corregir el servidor sin bloquear la pantalla.
            builder!.ConnectTimeout = 5;
            await using var connection = new SqlConnection(builder.ConnectionString);
            await connection.OpenAsync();
            Status = $"Conexión correcta con {connection.Database}.";
        }
        catch (Exception ex)
        {
            // Se muestra el error del proveedor, nunca la cadena ni la contraseña.
            Status = $"No se pudo conectar: {ex.Message}";
        }
        finally { IsBusy = false; }
    }

    /// <summary>Guarda la dirección SQL local y avisa que los contextos abiertos requieren reinicio.</summary>
    private void SaveConnection()
    {
        if (!CanManageConnection)
        {
            Status = "Solo un administrador puede configurar la conexión.";
            return;
        }

        if (!TryBuild(ConnectionString, out var builder))
        {
            Status = "Indique Server y Database en una cadena SQL válida.";
            return;
        }

        try
        {
            // SqlConnectionStringBuilder normaliza claves equivalentes antes de guardar.
            _store.Save(_store.Load() with { ConnectionString = builder!.ConnectionString });
            ConnectionString = builder.ConnectionString;
            Status = "Conexión guardada para este equipo. Reinicie para aplicarla a todas las pantallas.";
            ConnectionSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Status = $"No se pudo guardar la configuración: {ex.Message}";
        }
    }

    /// <summary>Abre una nueva instancia y cierra la actual para renovar todos los DbContext.</summary>
    private void Restart()
    {
        if (!CanManageConnection)
        {
            Status = "Solo un administrador puede configurar la conexión.";
            return;
        }

        string? executable = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executable))
        {
            Status = "Cierre y vuelva a abrir la aplicación para aplicar la conexión.";
            return;
        }
        try
        {
            // El nuevo proceso relee la conexión y crea contextos SQL nuevos.
            var start = new ProcessStartInfo(executable) { UseShellExecute = true };
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            {
                // En desarrollo la aplicación puede ejecutarse como DLL sin apphost.
                start.UseShellExecute = false;
                start.ArgumentList.Add("exec");
                start.ArgumentList.Add(typeof(App).Assembly.Location);
            }
            Process.Start(start);
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Status = $"No se pudo reiniciar la aplicación: {ex.Message}";
        }
    }
}
