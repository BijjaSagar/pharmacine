using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Npgsql;
using PharmacySystem.Desktop.Helpers;
using PharmacySystem.Desktop.Models;
using PharmacySystem.Desktop.Services;

namespace PharmacySystem.Desktop.ViewModels
{
    public class UserViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;

        public ObservableCollection<User> Users { get; set; } = new();

        private User _selectedUser;
        public User SelectedUser
        {
            get => _selectedUser;
            set
            {
                SetProperty(ref _selectedUser, value);
                if (value != null)
                {
                    Username = value.Username;
                    Role = value.Role;
                    IsActive = value.IsActive;
                }
            }
        }

        private string _username = string.Empty;
        public string Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        private string _password = string.Empty;
        public string Password
        {
            get => _password;
            set => SetProperty(ref _password, value);
        }

        private string _role = "Biller";
        public string Role
        {
            get => _role;
            set => SetProperty(ref _role, value);
        }

        private bool _isActive = true;
        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }
        
        public ObservableCollection<string> Roles { get; } = new() { "Admin", "Manager", "Biller" };

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand ClearCommand { get; }

        public UserViewModel()
        {
            _dbService = new DatabaseService();
            SaveCommand = new RelayCommand(async _ => await SaveUserAsync());
            ClearCommand = new RelayCommand(_ => ClearForm());
            _ = LoadUsersAsync();
        }

        private async Task LoadUsersAsync()
        {
            IsBusy = true;
            ErrorMessage = string.Empty;
            Users.Clear();

            try
            {
                var dt = await _dbService.ExecuteQueryAsync("SELECT user_id, username, role, is_active FROM users ORDER BY username");
                foreach (System.Data.DataRow row in dt.Rows)
                {
                    Users.Add(new User
                    {
                        UserId = Convert.ToInt32(row["user_id"]),
                        Username = row["username"].ToString() ?? "",
                        Role = row["role"].ToString() ?? "",
                        IsActive = Convert.ToBoolean(row["is_active"])
                    });
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Load failed: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveUserAsync()
        {
            if (string.IsNullOrWhiteSpace(Username))
            {
                ErrorMessage = "Username is required.";
                return;
            }

            IsBusy = true;
            try
            {
                if (SelectedUser == null)
                {
                    // Create New
                    if (string.IsNullOrWhiteSpace(Password))
                    {
                        ErrorMessage = "Password is required for new users.";
                        IsBusy = false;
                        return;
                    }
                    
                    var sql = "INSERT INTO users (username, password_hash, role, is_active) VALUES (@un, @pw, @role, @act)";
                    await _dbService.ExecuteNonQueryAsync(sql,
                        new NpgsqlParameter("@un", Username),
                        new NpgsqlParameter("@pw", Password), // Note: Hash in prod
                        new NpgsqlParameter("@role", Role),
                        new NpgsqlParameter("@act", IsActive));
                }
                else
                {
                    // Update
                    if (!string.IsNullOrWhiteSpace(Password))
                    {
                        var sql = "UPDATE users SET username = @un, password_hash = @pw, role = @role, is_active = @act WHERE user_id = @id";
                        await _dbService.ExecuteNonQueryAsync(sql,
                            new NpgsqlParameter("@un", Username),
                            new NpgsqlParameter("@pw", Password),
                            new NpgsqlParameter("@role", Role),
                            new NpgsqlParameter("@act", IsActive),
                            new NpgsqlParameter("@id", SelectedUser.UserId));
                    }
                    else
                    {
                        var sql = "UPDATE users SET username = @un, role = @role, is_active = @act WHERE user_id = @id";
                        await _dbService.ExecuteNonQueryAsync(sql,
                            new NpgsqlParameter("@un", Username),
                            new NpgsqlParameter("@role", Role),
                            new NpgsqlParameter("@act", IsActive),
                            new NpgsqlParameter("@id", SelectedUser.UserId));
                    }
                }

                ClearForm();
                await LoadUsersAsync();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Save failed: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ClearForm()
        {
            SelectedUser = null;
            Username = string.Empty;
            Password = string.Empty;
            Role = "Biller";
            IsActive = true;
            ErrorMessage = string.Empty;
        }
    }
}
