using Avalonia.Controls;
using PharmacySystem.Desktop.ViewModels;

namespace PharmacySystem.Desktop.Views
{
    public partial class LicenseActivationView : Window
    {
        public LicenseActivationView()
        {
            InitializeComponent();
            
            if (DataContext is LicenseActivationViewModel vm)
            {
                vm.OnActivated = () =>
                {
                    var login = new LoginView();
                    login.Show();
                    this.Close();
                };
            }
        }
    }
}
