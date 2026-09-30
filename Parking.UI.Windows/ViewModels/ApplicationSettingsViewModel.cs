using Microsoft.Data.SqlClient;
using Parking.Application.Services;
using Parking.UI.Windows.Services;
using Parking.UI.Windows.ViewModels.Base;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

/// <summary>Permite escoger el tema y configurar la base SQL de este equipo.</summary>
public sealed class ApplicationSettingsViewModel : BaseViewModel
{
    private readonly ApplicationSettingsStore _store;
    private readonly ThemeService _themeService;
    private string _connectionString;
    private string _currencySymbol;
    private string _status = string.Empty;
    private bool _isBusy;

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

    /// <summary>Prepara preferencias y comandos sin abrir todavía SQL.</summary>
    /// <param name="store">Almacén cifrado local.</param>
    /// <param name="themeService">Control del tema global.</param>
    public ApplicationSettingsViewModel(ApplicationSettingsStore store, ThemeService themeService)
    {
        _store = store;
        _themeService = themeService;
        _currencySymbol = store.Load().CurrencySymbol;
        // Los operadores no reciben la cadena, que podría contener una contraseña SQL.
        _connectionString = CanManageConnection ? store.Load().ConnectionString : string.Empty;
        TestConnectionCommand = new AsyncRelayCommand(_ => TestConnectionAsync());
        SaveConnectionCommand = new RelayCommand(_ => SaveConnection());
        RestartCommand = new RelayCommand(_ => Restart());
        SaveCurrencyCommand = new RelayCommand(_ => SaveCurrency());
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
