using System;
using System.Threading.Tasks;
using System.Windows.Input;
using PharmacySystem.Desktop.Helpers;

namespace PharmacySystem.Desktop.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ICommand BackupDatabaseCommand { get; }

        public SettingsViewModel()
        {
            BackupDatabaseCommand = new RelayCommand(async _ => await RunBackupAsync());
        }

        private async Task RunBackupAsync()
        {
            IsBusy = true;
            StatusMessage = "Running backup... please wait.";
            try
            {
                string path = await BackupHelper.BackupDatabaseAsync();
                StatusMessage = $"Backup successful!\nSaved to: {path}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Backup failed: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
