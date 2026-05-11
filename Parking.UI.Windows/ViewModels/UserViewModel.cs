using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Input;

namespace Parking.UI.Windows.ViewModels;

public class UserViewModel : BaseViewModel
{
    private readonly IUserRepository _repository;
    private readonly int _currentUserId = 1;

    public ObservableCollection<User> Users { get; } = new();

    public ICommand SaveCommand { get; }
    public ICommand UpdateCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand NewCommand { get; }

    public UserViewModel(IUserRepository repository)
    {
        _repository = repository;

        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        UpdateCommand = new RelayCommand(async _ => await UpdateAsync());
        DeleteCommand = new RelayCommand(async _ => await DeleteAsync());
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

    private string _role = string.Empty;
    public string Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
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

    private User? _selectedUser;
    public User? SelectedUser
    {
        get => _selectedUser;
        set
        {
            if (SetProperty(ref _selectedUser, value) && value != null)
                LoadSelected(value);
        }
    }

    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        Users.Clear();

        var items = await _repository.GetAllAsync();

        foreach (var item in items)
            Users.Add(item);
    }

    private void LoadSelected(User item)
    {
        Id = item.Id;
        Username = item.Username ?? string.Empty;
        FullName = item.FullName ?? string.Empty;
        Role = item.Role ?? string.Empty;
        IsActive = item.IsActive;

        Password = string.Empty;
        ConfirmPassword = string.Empty;
    }

    private bool Validate(bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            StatusMessage = "Ingrese el usuario.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(FullName))
        {
            StatusMessage = "Ingrese el nombre.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Role))
        {
            StatusMessage = "Ingrese el rol.";
            return false;
        }

        if (requirePassword)
        {
            if (string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Ingrese la contraseña.";
                return false;
            }

            if (Password != ConfirmPassword)
            {
                StatusMessage = "Las contraseñas no coinciden.";
                return false;
            }
        }

        return true;
    }

    private async Task SaveAsync()
    {
        if (!Validate(true))
            return;

        var entity = new User
        {
            Username = Username.Trim(),
            FullName = FullName.Trim(),
            Role = Role.Trim(),
            PasswordHash = HashPassword(Password),
            IsActive = IsActive,
            CreatedAt = DateTime.Now,
            CreatedBy = _currentUserId,
            IsDeleted = false,
            LoginAttempts = 0
        };

        await _repository.AddAsync(entity);
        await _repository.SaveChangesAsync();

        StatusMessage = "Usuario registrado.";

        await LoadAsync();
        ClearForm();
    }

    private async Task UpdateAsync()
    {
        if (Id == 0)
        {
            StatusMessage = "Seleccione un registro.";
            return;
        }

        var entity = await _repository.GetByIdAsync(Id);

        if (entity == null)
        {
            StatusMessage = "Usuario no encontrado.";
            return;
        }

        entity.Username = Username.Trim();
        entity.FullName = FullName.Trim();
        entity.Role = Role.Trim();
        entity.IsActive = IsActive;
        entity.UpdatedAt = DateTime.Now;
        entity.UpdatedBy = _currentUserId;

        if (!string.IsNullOrWhiteSpace(Password))
        {
            if (Password != ConfirmPassword)
            {
                StatusMessage = "Las contraseñas no coinciden.";
                return;
            }

            entity.PasswordHash = HashPassword(Password);
        }

        await _repository.UpdateAsync(entity);
        await _repository.SaveChangesAsync();

        StatusMessage = "Usuario actualizado.";

        await LoadAsync();
        ClearForm();
    }

    private async Task DeleteAsync()
    {
        if (Id == 0)
        {
            StatusMessage = "Seleccione un registro.";
            return;
        }

        var entity = await _repository.GetByIdAsync(Id);

        if (entity == null)
        {
            StatusMessage = "Usuario no encontrado.";
            return;
        }

        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.Now;
        entity.DeletedBy = _currentUserId;

        await _repository.UpdateAsync(entity);
        await _repository.SaveChangesAsync();

        StatusMessage = "Usuario eliminado.";

        await LoadAsync();
        ClearForm();
    }

    private void ClearForm()
    {
        Id = 0;
        Username = string.Empty;
        FullName = string.Empty;
        Role = string.Empty;
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        IsActive = true;
        SelectedUser = null;
    }

    private string HashPassword(string password)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToHexString(bytes);
    }
}