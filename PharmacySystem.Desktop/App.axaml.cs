using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PharmacySystem.Desktop.Views;

namespace PharmacySystem.Desktop
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var status = PharmacySystem.Desktop.Services.LicenseService.CheckLicense();
                if (status.IsValid)
                {
                    desktop.MainWindow = new LoginView();
                }
                else
                {
                    desktop.MainWindow = new LicenseActivationView();
                }
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}
