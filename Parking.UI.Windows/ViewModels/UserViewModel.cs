using Parking.UI.Windows.Interfaces;
using Parking.Application.Services;
using Parking.Application.Interfaces;
using Parking.Domain.Model.Enums;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.ViewModels.Base;
using Parking.UI.Windows.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;


namespace Parking.UI.Windows.ViewModels;


public class userViewModel : BaseViewModel
{

    private readonly IUserManagementService _users;

    private readonly IDialogService _dialogs;


    private readonly int _currentuserId = CurrentUser.Id;



    public List<userRole> Roles { get; } =
        Enum.GetValues(typeof(userRole))
        .Cast<userRole>()
        .ToList();



    public ObservableCollection<user> users { get; } = new();



    public ICommand SaveCommand { get; }

    public ICommand UpdateCommand { get; }

    public ICommand DeleteCommand { get; }

    public ICommand NewCommand { get; }




    public userViewModel(
        IUserManagementService users,
        IDialogService dialogs)
    {

        _users = users;

        _dialogs = dialogs;



        SaveCommand =
            new RelayCommand(
                async _ => await SaveAsync());


        UpdateCommand =
            new RelayCommand(
                async _ => await UpdateAsync());


        DeleteCommand =
            new RelayCommand(
                async _ => await DeleteAsync());


        NewCommand =
            new RelayCommand(
                _ => ClearForm());

    }





    private int _id;

    public int Id
    {
        get => _id;

        set => SetProperty(ref _id, value);
    }





    private string _username = string.Empty;

    public string username
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





    private userRole _selectedRole = userRole.Operador;


    public userRole SelectedRole
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





    private user? _selecteduser;


    public user? Selecteduser
    {
        get => _selecteduser;

        set
        {
            if (SetProperty(ref _selecteduser, value)
               && value != null)
            {
                LoadSelected(value);
            }
        }
    }





    public async Task InitializeAsync()
    {
        await LoadAsync();
    }

    private async Task LoadAsync()
    {

        users.Clear();


        var items =
            await _users.GetAllAsync();



        foreach (var item in items)
            users.Add(item);

    }

    private void LoadSelected(user item)
    {

        Id = item.id;

        username = item.username;

        FullName = item.full_name;


        if (Enum.TryParse(
            item.role,
            out userRole role))
        {
            SelectedRole = role;
        }


        IsActive = item.is_active;


        Password = string.Empty;

        ConfirmPassword = string.Empty;

    }

    private bool Validate(bool requirePassword)
    {

        if (string.IsNullOrWhiteSpace(username))
        {
            StatusMessage = "Ingrese el usuario.";
            _dialogs.ShowWarning("Usuarios", StatusMessage);
            return false;
        }



        if (string.IsNullOrWhiteSpace(FullName))
        {
            StatusMessage = "Ingrese el nombre.";
            _dialogs.ShowWarning("Usuarios", StatusMessage);
            return false;
        }



        if (requirePassword)
        {

            if (string.IsNullOrWhiteSpace(Password))
            {
                StatusMessage = "Ingrese la contraseña.";
                _dialogs.ShowWarning("Usuarios", StatusMessage);
                return false;
            }

            if (Password.Length < 8)
            {
                StatusMessage = "La contraseña debe tener al menos 8 caracteres.";
                _dialogs.ShowWarning("Usuarios", StatusMessage);
                return false;
            }


            if (Password != ConfirmPassword)
            {
                StatusMessage =
                    "Las contraseñas no coinciden.";
                _dialogs.ShowWarning("Usuarios", StatusMessage);

                return false;
            }

        }


        return true;
    }

    private async Task SaveAsync()
    {

        try
        {

            if (!Validate(true))
                return;



            var entity = new user
            {

                username =
                    username.Trim(),


                full_name =
                    FullName.Trim(),


                role =
                    SelectedRole.ToString(),



                is_active =
                    IsActive,


                created_at =
                    DateTime.Now,


                created_by =
                    _currentuserId,


                login_attempts = 0,


                is_deleted = false

            };



            if (!await _users.CreateAsync(entity, Password))
            {
                StatusMessage = "El usuario ya existe.";
                _dialogs.ShowWarning("Usuarios", StatusMessage);
                return;
            }



            StatusMessage =
                "Usuario registrado correctamente.";
            _dialogs.ShowSuccess("Usuarios", StatusMessage);



            await LoadAsync();



            ClearForm();

        }
        catch (Exception ex)
        {

            System.Diagnostics.Debug.WriteLine($"Error al registrar usuario: {ex}");
            StatusMessage = "No se pudo registrar el usuario. Revise los datos e inténtelo de nuevo.";
            _dialogs.ShowError("Usuarios", StatusMessage);

        }

    }

    private async Task UpdateAsync()
    {

        if (Id == 0)
        {
            StatusMessage =
                "Seleccione un usuario.";
            _dialogs.ShowWarning("Usuarios", StatusMessage);

            return;
        }



        var entity =
            await _users.GetByIdAsync(Id);



        if (entity == null)
        {
            StatusMessage =
                "Usuario no encontrado.";
            _dialogs.ShowWarning("Usuarios", StatusMessage);

            return;
        }



        entity.username =
            username.Trim();


        entity.full_name =
            FullName.Trim();


        entity.role =
            SelectedRole.ToString();



        entity.is_active =
            IsActive;



        entity.updated_at =
            DateTime.Now;



        entity.updated_by =
            _currentuserId;



        if (!string.IsNullOrWhiteSpace(Password))
        {

            if (Password.Length < 8)
            {
                StatusMessage = "La contraseña debe tener al menos 8 caracteres.";
                _dialogs.ShowWarning("Usuarios", StatusMessage);
                return;
            }

            if (Password != ConfirmPassword)
            {
                StatusMessage =
                    "Las contraseñas no coinciden.";
                _dialogs.ShowWarning("Usuarios", StatusMessage);

                return;
            }



        }

        await _users.UpdateAsync(entity, Password);



        StatusMessage =
            "Usuario actualizado.";
        _dialogs.ShowSuccess("Usuarios", StatusMessage);



        await LoadAsync();


        ClearForm();

    }

    private async Task DeleteAsync()
    {

        if (Id == 0)
        {
            StatusMessage =
                "Seleccione un usuario.";
            _dialogs.ShowWarning("Usuarios", StatusMessage);

            return;
        }



        var entity =
            await _users.GetByIdAsync(Id);



        if (entity == null)
        {
            StatusMessage =
                "Usuario no encontrado.";
            _dialogs.ShowWarning("Usuarios", StatusMessage);

            return;
        }



        await _users.DeleteAsync(entity, _currentuserId);

        StatusMessage =
            "Usuario eliminado.";
        _dialogs.ShowSuccess("Usuarios", StatusMessage);



        await LoadAsync();


        ClearForm();

    }

    private void ClearForm()
    {

        Id = 0;

        username = string.Empty;

        FullName = string.Empty;

        SelectedRole =
            userRole.Operador;


        Password = string.Empty;


        ConfirmPassword =
            string.Empty;


        IsActive = true;


        Selecteduser = null;

    }

}
