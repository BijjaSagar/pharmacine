using Avalonia.Controls;
using Avalonia.Input;
using PharmacySystem.Desktop.ViewModels;

namespace PharmacySystem.Desktop.Views
{
    public partial class POSView : Window
    {
        public POSView()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Global keyboard shortcuts for the POS window.
        /// F2  → Focus the search box
        /// F5  → Checkout
        /// F8  → Cancel bill
        /// Ctrl+1 … Ctrl+0 → Switch to Console 1–10
        /// Escape → Close search results
        /// </summary>
        private void Window_KeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not SaleViewModel vm) return;

            // F2 — focus search
            if (e.Key == Key.F2)
            {
                var box = this.FindControl<TextBox>("SearchBox");
                box?.Focus();
                e.Handled = true;
                return;
            }

            // F5 — checkout
            if (e.Key == Key.F5)
            {
                if (vm.CheckoutCommand.CanExecute(null))
                    vm.CheckoutCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // F9 — substitute search
            if (e.Key == Key.F9)
            {
                if (vm.SubstituteSearchCommand.CanExecute(null))
                    vm.SubstituteSearchCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // F8 — cancel bill
            if (e.Key == Key.F8)
            {
                if (vm.CancelBillCommand.CanExecute(null))
                    vm.CancelBillCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Escape — clear search
            if (e.Key == Key.Escape)
            {
                if (vm.ClearSearchCommand.CanExecute(null))
                    vm.ClearSearchCommand.Execute(null);
                e.Handled = true;
                return;
            }

            // Ctrl+1 … Ctrl+9, Ctrl+0 (for Console 10) 
            if (e.KeyModifiers == KeyModifiers.Control)
            {
                int consoleNum = e.Key switch
                {
                    Key.D1 => 1,
                    Key.D2 => 2,
                    Key.D3 => 3,
                    Key.D4 => 4,
                    Key.D5 => 5,
                    Key.D6 => 6,
                    Key.D7 => 7,
                    Key.D8 => 8,
                    Key.D9 => 9,
                    Key.D0 => 10,
                    _ => -1
                };

                if (consoleNum != -1)
                {
                    vm.SwitchConsoleCommand.Execute(consoleNum);
                    vm.StatusMessage = $"Switched to Console {consoleNum}";
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Enter in search box → Add selected item to cart.
        /// Arrow keys → Move selection in search results.
        /// </summary>
        private void SearchBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not SaleViewModel vm) return;

            if (e.Key == Key.Enter)
            {
                if (vm.AddToCartCommand.CanExecute(null))
                    vm.AddToCartCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.F9)
            {
                if (vm.SubstituteSearchCommand.CanExecute(null))
                    vm.SubstituteSearchCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Down)
            {
                // Move selection to the search list
                var list = this.FindControl<ListBox>("SearchList");
                if (list != null && list.ItemCount > 0)
                {
                    list.Focus();
                    list.SelectedIndex = 0;
                }
                e.Handled = true;
            }
        }

        /// <summary>
        /// Enter in search list → Add selected item.
        /// </summary>
        private void SearchList_KeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not SaleViewModel vm) return;

            if (e.Key == Key.Enter)
            {
                if (vm.AddToCartCommand.CanExecute(null))
                    vm.AddToCartCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                vm.ClearSearchCommand.Execute(null);
                var box = this.FindControl<TextBox>("SearchBox");
                box?.Focus();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Delete key in cart grid → Remove selected row.
        /// </summary>
        private void CartGrid_KeyDown(object? sender, KeyEventArgs e)
        {
            if (DataContext is not SaleViewModel vm) return;
            if (e.Key != Key.Delete) return;

            var grid = this.FindControl<DataGrid>("CartGrid");
            if (grid?.SelectedItem is CartItem item)
            {
                vm.RemoveFromCartCommand.Execute(item);
                e.Handled = true;
            }
        }
    }
}
