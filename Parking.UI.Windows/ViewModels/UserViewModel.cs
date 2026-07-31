using Parking.Application.Services;
using Parking.Domain.Model.Abstractions;
using Parking.Domain.Model.Enums;
using Parking.Domain.Model.Models;
using Parking.UI.Windows.ViewModels.Base;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;


namespace Parking.UI.Windows.ViewModels;


public class userViewModel : BaseViewModel
{

    private readonly IuserRepository _repository;

    private readonly IPasswordHasher _passwordHasher;


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
        IuserRepository repository,
        IPasswordHasher passwordHasher)
    {

        _repository = repository;

        _passwordHasher = passwordHasher;



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
            await _repository.GetAllAsync();



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
            return false;
        }



        if (string.IsNullOrWhiteSpace(FullName))
        {
            StatusMessage = "Ingrese el nombre.";
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
                StatusMessage =
                    "Las contraseñas no coinciden.";

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



            bool exists =
                await _repository
                .ExistsByusernameAsync(username);



            if (exists)
            {
                StatusMessage =
                    "El usuario ya existe.";

                return;
            }




            var entity = new user
            {

                username =
                    username.Trim(),


                full_name =
                    FullName.Trim(),


                role =
                    SelectedRole.ToString(),



                // NUEVO SISTEMA DE HASH
                password_hash =
                    _passwordHasher
                    .HashPassword(Password),



                is_active =
                    IsActive,


                created_at =
                    DateTime.Now,


                created_by =
                    _currentuserId,


                login_attempts = 0,


                is_deleted = false

            };



            await _repository.AddAsync(entity);


            await _repository.SaveChangesAsync();



            StatusMessage =
                "Usuario registrado correctamente.";



            await LoadAsync();



            ClearForm();

        }
        catch (Exception ex)
        {

            StatusMessage = ex.Message;


            MessageBox.Show(
                ex.ToString(),
                "ERROR",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

        }

    }

    private async Task UpdateAsync()
    {

        if (Id == 0)
        {
            StatusMessage =
                "Seleccione un usuario.";

            return;
        }



        var entity =
            await _repository.GetByIdAsync(Id);



        if (entity == null)
        {
            StatusMessage =
                "Usuario no encontrado.";

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

            if (Password != ConfirmPassword)
            {
                StatusMessage =
                    "Las contraseñas no coinciden.";

                return;
            }



            entity.password_hash =
                _passwordHasher
                .HashPassword(Password);

        }




        await _repository.UpdateAsync(entity);


        await _repository.SaveChangesAsync();



        StatusMessage =
            "Usuario actualizado.";



        await LoadAsync();


        ClearForm();

    }

    private async Task DeleteAsync()
    {

        if (Id == 0)
        {
            StatusMessage =
                "Seleccione un usuario.";

            return;
        }



        var entity =
            await _repository.GetByIdAsync(Id);



        if (entity == null)
        {
            StatusMessage =
                "Usuario no encontrado.";

            return;
        }



        entity.is_deleted = true;


        entity.deleted_at =
            DateTime.Now;


        entity.deleted_by =
            _currentuserId;



        await _repository.UpdateAsync(entity);



        StatusMessage =
            "Usuario eliminado.";



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