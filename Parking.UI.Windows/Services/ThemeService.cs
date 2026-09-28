using MaterialDesignThemes.Wpf;
using System.Windows;
using System.Windows.Media;

namespace Parking.UI.Windows.Services;

/// <summary>Aplica los colores comunes y el tema Material Design a todas las ventanas.</summary>
public sealed class ThemeService
{
    private readonly ApplicationSettingsStore _settingsStore;

    /// <summary>Tema que está activo en la sesión.</summary>
    public AppThemeMode Current { get; private set; } = AppThemeMode.Dark;

    /// <summary>Recibe el almacén compartido de preferencias.</summary>
    /// <param name="settingsStore">Persistencia del tema y de la conexión.</param>
    public ThemeService(ApplicationSettingsStore settingsStore) => _settingsStore = settingsStore;

    /// <summary>Restaura el tema guardado antes de mostrar el login.</summary>
    public void ApplyStored() => Apply(_settingsStore.Load().Theme, save: false);

    /// <summary>Cambia el tema de la sesión y lo guarda cuando se solicita.</summary>
    /// <param name="mode">Tema claro u oscuro.</param>
    /// <param name="save">Indica si debe persistirse para el próximo inicio.</param>
    public void Apply(AppThemeMode mode, bool save = true)
    {
        // La paleta del paquete afecta a sus controles nativos.
        var helper = new PaletteHelper();
        var palette = helper.GetTheme();
        palette.SetBaseTheme(mode == AppThemeMode.Light ? BaseTheme.Light : BaseTheme.Dark);
        helper.SetTheme(palette);

        // Los pinceles compartidos recolorean los estilos propios del proyecto.
        SetBrush("AppPageBrush", mode, "#121212", "#F4F7FB");
        SetBrush("AppSurfaceBrush", mode, "#1E1E1E", "#FFFFFF");
        SetBrush("AppRaisedBrush", mode, "#252525", "#EAF0F5");
        SetBrush("AppSidebarBrush", mode, "#1A1A1A", "#E5EBF1");
        SetBrush("AppInputBrush", mode, "#0A0A0A", "#FFFFFF");
        SetBrush("AppCameraPanelBrush", mode, "#101820", "#FFFFFF");
        SetBrush("AppBorderBrush", mode, "#333333", "#C7D2DD");
        SetBrush("AppInputBorderBrush", mode, "#444444", "#A8B6C5");
        SetBrush("AppTextBrush", mode, "#FFFFFF", "#1D2935");
        SetBrush("AppMutedTextBrush", mode, "#AAB8C4", "#526173");
        SetBrush("AppAccentTextBrush", mode, "#4FC3F7", "#075F95");
        SetBrush("AppSelectedBrush", mode, "#15293A", "#DCECF8");
        Current = mode;

        if (save)
        {
            // Una modificación visual conserva intacta la conexión SQL guardada.
            var settings = _settingsStore.Load();
            _settingsStore.Save(settings with { Theme = mode });
        }
    }

    /// <summary>Actualiza un pincel que ya utilizan las vistas abiertas.</summary>
    /// <param name="key">Clave del recurso compartido.</param>
    /// <param name="mode">Tema solicitado.</param>
    /// <param name="dark">Color del tema oscuro.</param>
    /// <param name="light">Color del tema claro.</param>
    private static void SetBrush(string key, AppThemeMode mode, string dark, string light)
    {
        // Reemplazar el recurso permite que DynamicResource se actualice al instante.
        System.Windows.Application.Current.Resources[key] = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(mode == AppThemeMode.Light ? light : dark));
    }
}
