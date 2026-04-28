using System.Windows.Input;
using PharmacySystem.Desktop.Services;
using PharmacySystem.Desktop.Helpers;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PharmacySystem.Desktop.ViewModels
{
    public class LicenseActivationViewModel : INotifyPropertyChanged
    {
        public string HardwareId { get; }

        private string _activationKey = string.Empty;
        public string ActivationKey
        {
            get => _activationKey;
            set { _activationKey = value; OnPropertyChanged(); }
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public ICommand ActivateCommand { get; }
        public System.Action? OnActivated { get; set; }

        public LicenseActivationViewModel()
        {
            HardwareId = LicenseService.GetHardwareId();
            var status = LicenseService.CheckLicense();
            StatusMessage = status.Message;

            ActivateCommand = new RelayCommand(_ => 
            {
                if (LicenseService.ActivateLicense(ActivationKey))
                {
                    StatusMessage = "Activation Successful! Restarting...";
                    OnActivated?.Invoke();
                }
                else
                {
                    StatusMessage = "Invalid License Key!";
                }
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
