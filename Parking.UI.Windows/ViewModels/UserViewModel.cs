using Parking.Application.Interfaces;
using Parking.Application.Services;
using Parking.Domain.Model.Enums;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.Interfaces;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

/// <summary>Presenta cuentas y envía las altas, cambios y bajas a Application.</summary>
public class UserViewModel : BaseViewModel
{
    private readonly IUserManagementService _users;
    private readonly IDialogService _dialogs;
    private readonly int _currentUserId = CurrentUser.Id;

    public List<UserRole> Roles { get; } = Enum.GetValues<UserRole>().ToList();
    public ObservableCollection<user> Users { get; } = new();
    public ICommand SaveCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand NewCommand { get; }

    /// <summary>Recibe el caso de uso de cuentas y prepara las acciones de la pantalla.</summary>
    public UserViewModel(IUserManagementService users, IDialogService dialogs)
    {
        _users = users;
        _dialogs = dialogs;
        SaveCommand = new AsyncRelayCommand(_ => SaveAsync());
        UpdateCommand = new AsyncRelayCommand(_ => RunSafelyAsync(UpdateAsync,
            "No se pudo actualizar el usuario. Inténtelo de nuevo."));
        DeleteCommand = new AsyncRelayCommand(_ => RunSafelyAsync(DeleteAsync,
            "No se pudo eliminar el usuario. Inténtelo de nuevo."));
        NewCommand = new RelayCommand(_ => ClearForm());
    }

    private int _id;
    public int Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    private string _username = string.Empty;
    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    private string _fullName = string.Empty;
    public string FullName
    {
        get => _fullName;
        set => SetProperty(ref _fullName, value);
    }

    private UserRole _selectedRole = UserRole.Operador;
    public UserRole SelectedRole
    {
        get => _selectedRole;
        set => SetProperty(ref _selectedRole, value);
    }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private string _confirmPassword = string.Empty;
    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    private bool _isActive = true;
    public bool IsActive
    {
        get => _isActive;
        set => SetProperty(ref _isActive, value);
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    private user? _selectedUser;
    public user? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value) && value != null)
                LoadSelected(value);
        }
    }

    /// <summary>Carga los usuarios cuando se abre la sección.</summary>
    public Task InitializeAsync() => LoadAsync();

    /// <summary>Actualiza la lista sin incluir usuarios eliminados.</summary>
    private async Task LoadAsync()
    {
        Users.Clear();
        var items = await _users.GetAllAsync();
        foreach (var item in items) Users.Add(item);
    }

    /// <summary>Copia a los campos editables la cuenta seleccionada.</summary>
    private void LoadSelected(user item)
    {
        Id = item.id;
        Username = item.username;
        FullName = item.full_name;
        if (Enum.TryParse(item.role, out UserRole role)) SelectedRole = role;
        IsActive = item.is_active;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
    }

    /// <summary>Valida la ficha y exige contraseña al crear una cuenta.</summary>
    private bool Validate(bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(Username))
            return ShowValidationError("Ingrese el usuario.");
        if (string.IsNullOrWhiteSpace(FullName))
            return ShowValidationError("Ingrese el nombre.");
        if (requirePassword && string.IsNullOrWhiteSpace(Password))
            return ShowValidationError("Ingrese la contraseña.");
        if (string.IsNullOrWhiteSpace(Password)) return true;
        if (Password.Length < 8)
            return ShowValidationError("La contraseña debe tener al menos 8 caracteres.");
        if (Password != ConfirmPassword)
            return ShowValidationError("Las contraseñas no coinciden.");
        return true;
    }

    /// <summary>Muestra el primer error de validación en el diálogo del sistema.</summary>
    private bool ShowValidationError(string message)
    {
        StatusMessage = message;
        _dialogs.ShowWarning("Usuarios", message);
        return false;
    }

    /// <summary>Prepara la cuenta; Application genera el hash de su contraseña.</summary>
    private user CreateUser() => new()
    {
        username = Username.Trim(),
        full_name = FullName.Trim(),
        role = SelectedRole.ToString(),
        is_active = IsActive,
        created_at = DateTime.Now,
        created_by = _currentUserId,
        login_attempts = 0,
        is_deleted = false
    };

    /// <summary>Registra una cuenta nueva y recarga la lista.</summary>
    private async Task SaveAsync()
    {
        try
        {
            if (!Validate(requirePassword: true)) return;
            if (!await _users.CreateAsync(CreateUser(), Password))
            {
                ShowValidationError("El usuario ya existe.");
                return;
            }

            StatusMessage = "Usuario registrado correctamente.";
            _dialogs.ShowSuccess("Usuarios", StatusMessage);
            await LoadAsync();
            ClearForm();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error al registrar usuario: {ex}");
            StatusMessage = "No se pudo registrar el usuario. Revise los datos e inténtelo de nuevo.";
            _dialogs.ShowError("Usuarios", StatusMessage);
        }
    }

    /// <summary>Actualiza la cuenta; una contraseña vacía conserva la actual.</summary>
    private async Task UpdateAsync()
    {
        if (Id == 0)
        {
            ShowValidationError("Seleccione un usuario.");
            return;
        }
        if (!Validate(requirePassword: false)) return;

        var entity = await _users.GetByIdAsync(Id);
        if (entity == null)
        {
            ShowValidationError("Usuario no encontrado.");
            return;
        }

        ApplyUserChanges(entity);
        await _users.UpdateAsync(entity, Password);
        StatusMessage = "Usuario actualizado.";
        _dialogs.ShowSuccess("Usuarios", StatusMessage);
        await LoadAsync();
        ClearForm();
    }

    /// <summary>Aplica los campos editables a la cuenta recuperada.</summary>
    private void ApplyUserChanges(user entity)
    {
        entity.username = Username.Trim();
        entity.full_name = FullName.Trim();
        entity.role = SelectedRole.ToString();
        entity.is_active = IsActive;
        entity.updated_at = DateTime.Now;
        entity.updated_by = _currentUserId;
    }

    /// <summary>Elimina lógicamente la cuenta seleccionada y recarga la lista.</summary>
    private async Task DeleteAsync()
    {
        if (Id == 0)
        {
            ShowValidationError("Seleccione un usuario.");
            return;
        }

        var entity = await _users.GetByIdAsync(Id);
        if (entity == null)
        {
            ShowValidationError("Usuario no encontrado.");
            return;
        }

        await _users.DeleteAsync(entity, _currentUserId);
        StatusMessage = "Usuario eliminado.";
        _dialogs.ShowSuccess("Usuarios", StatusMessage);
        await LoadAsync();
        ClearForm();
    }

    /// <summary>Presenta fallos de actualización y eliminación con el diálogo habitual.</summary>
    private async Task RunSafelyAsync(Func<Task> action, string message)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error en la gestión de usuarios: {ex}");
            StatusMessage = message;
            _dialogs.ShowError("Usuarios", message);
        }
    }

    /// <summary>Limpia los campos para ingresar otra cuenta.</summary>
    private void ClearForm()
    {
        Id = 0;
        Username = string.Empty;
        FullName = string.Empty;
        SelectedRole = UserRole.Operador;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        IsActive = true;
        SelectedUser = null;
    }
}
