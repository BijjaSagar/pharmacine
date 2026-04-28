using PharmacySystem.Desktop.Helpers;
using PharmacySystem.Desktop.Models;
using PharmacySystem.Desktop.Services;
using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Input;
using Npgsql;
using System.Windows;

namespace PharmacySystem.Desktop.ViewModels
{
    public class SupplierViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;

        public ObservableCollection<Supplier> Suppliers { get; } = new ObservableCollection<Supplier>();

        private Supplier _selectedSupplier = new Supplier();
        public Supplier SelectedSupplier
        {
            get => _selectedSupplier;
            set => SetProperty(ref _selectedSupplier, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        public ICommand LoadCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand ClearCommand { get; }

        public SupplierViewModel()
        {
            _dbService = new DatabaseService();
            LoadCommand = new RelayCommand(async _ => await LoadSuppliersAsync());
            SaveCommand = new RelayCommand(async _ => await SaveSupplierAsync(), _ => !IsBusy);
            ClearCommand = new RelayCommand(_ => SelectedSupplier = new Supplier());
            
            _ = LoadSuppliersAsync();
        }

        private async Task LoadSuppliersAsync()
        {
            IsBusy = true;
            try
            {
                var dt = await _dbService.ExecuteQueryAsync("SELECT * FROM suppliers ORDER BY name LIMIT 500");
                Suppliers.Clear();
                foreach (DataRow row in dt.Rows)
                {
                    Suppliers.Add(new Supplier
                    {
                        SupplierId = Convert.ToInt32(row["supplier_id"]),
                        Name = row["name"].ToString() ?? "",
                        Gst = row["gst"]?.ToString() ?? "",
                        ContactPerson = row["contact_person"]?.ToString() ?? "",
                        Phone = row["phone"]?.ToString() ?? "",
                        Email = row["email"]?.ToString() ?? "",
                        Address = row["address"]?.ToString() ?? "",
                        IsActive = Convert.ToBoolean(row["is_active"])
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Loading Suppliers");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveSupplierAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedSupplier.Name))
            {
                MessageBox.Show("Supplier Name is required.", "Validation Error");
                return;
            }

            IsBusy = true;
            try
            {
                if (SelectedSupplier.SupplierId == 0)
                {
                    var sql = @"INSERT INTO suppliers (name, gst, contact_person, phone, email, address, is_active) 
                                VALUES (@name, @gst, @cp, @phone, @email, @addr, @ia)";
                    await _dbService.ExecuteNonQueryAsync(sql,
                        new NpgsqlParameter("@name", SelectedSupplier.Name),
                        new NpgsqlParameter("@gst", SelectedSupplier.Gst),
                        new NpgsqlParameter("@cp", SelectedSupplier.ContactPerson),
                        new NpgsqlParameter("@phone", SelectedSupplier.Phone),
                        new NpgsqlParameter("@email", SelectedSupplier.Email),
                        new NpgsqlParameter("@addr", SelectedSupplier.Address),
                        new NpgsqlParameter("@ia", SelectedSupplier.IsActive));
                }
                else
                {
                    var sql = @"UPDATE suppliers SET name=@name, gst=@gst, contact_person=@cp, phone=@phone, email=@email, 
                                address=@addr, is_active=@ia, last_modified=CURRENT_TIMESTAMP 
                                WHERE supplier_id=@id";
                    await _dbService.ExecuteNonQueryAsync(sql,
                        new NpgsqlParameter("@id", SelectedSupplier.SupplierId),
                        new NpgsqlParameter("@name", SelectedSupplier.Name),
                        new NpgsqlParameter("@gst", SelectedSupplier.Gst),
                        new NpgsqlParameter("@cp", SelectedSupplier.ContactPerson),
                        new NpgsqlParameter("@phone", SelectedSupplier.Phone),
                        new NpgsqlParameter("@email", SelectedSupplier.Email),
                        new NpgsqlParameter("@addr", SelectedSupplier.Address),
                        new NpgsqlParameter("@ia", SelectedSupplier.IsActive));
                }
                
                await LoadSuppliersAsync();
                SelectedSupplier = new Supplier(); 
                MessageBox.Show("Supplier Saved Successfully!", "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Saving Supplier");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
