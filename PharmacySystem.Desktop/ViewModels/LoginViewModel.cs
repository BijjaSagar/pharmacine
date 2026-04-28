using PharmacySystem.Desktop.Helpers;
using PharmacySystem.Desktop.Models;
using PharmacySystem.Desktop.Services;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Npgsql;

namespace PharmacySystem.Desktop.ViewModels
{
    public class LoginViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;

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

        public ICommand LoginCommand { get; }

        public LoginViewModel()
        {
            _dbService = new DatabaseService();
            LoginCommand = new RelayCommand(async _ => await LoginAsync(), _ => !IsBusy);
        }

        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                ErrorMessage = "Please enter both username and password.";
                return;
            }

            IsBusy = true;
            ErrorMessage = string.Empty;

            try
            {
                var sql = "SELECT user_id, username, role, is_active FROM users WHERE username = @user AND password_hash = @pass";
                var dt = await _dbService.ExecuteQueryAsync(sql, 
                    new NpgsqlParameter("@user", Username),
                    new NpgsqlParameter("@pass", Password)); // Note: Needs hashing for production

                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    var isActive = Convert.ToBoolean(row["is_active"]);

                    if (!isActive)
                    {
                        ErrorMessage = "Account is disabled. Contact Administrator.";
                    }
                    else
                    {
                        AppSession.UserId = Convert.ToInt32(row["user_id"]);
                        AppSession.Username = row["username"].ToString() ?? string.Empty;
                        AppSession.Role = row["role"].ToString() ?? string.Empty;

                        OpenMainApp();
                    }
                }
                else
                {
                    ErrorMessage = "Invalid username or password.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "Database connection failed. " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OpenMainApp()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var dashboardView = new Views.MainDashboardView();
                dashboardView.Show();
                desktop.MainWindow?.Close();
                desktop.MainWindow = dashboardView;
            }
        }
    }
}
