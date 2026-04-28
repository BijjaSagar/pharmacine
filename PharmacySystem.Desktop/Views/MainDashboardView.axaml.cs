using Avalonia.Controls;
using Avalonia.Input;
using PharmacySystem.Desktop.ViewModels;

namespace PharmacySystem.Desktop.Views
{
    public partial class MainDashboardView : Window
    {
        public MainDashboardView()
        {
            InitializeComponent();
            this.KeyDown += MainDashboardView_KeyDown;
        }

        private void MainDashboardView_KeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not MainDashboardViewModel vm) return;

            switch (e.Key)
            {
                case Key.F1:
                    if (vm.OpenPosCommand.CanExecute(null)) vm.OpenPosCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F2:
                    if (vm.OpenProductsCommand.CanExecute(null)) vm.OpenProductsCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F3:
                    if (vm.OpenSuppliersCommand.CanExecute(null)) vm.OpenSuppliersCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F4:
                    if (vm.OpenPurchaseCommand.CanExecute(null)) vm.OpenPurchaseCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F5:
                    if (vm.OpenReportsCommand.CanExecute(null)) vm.OpenReportsCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
        }
    }
}
