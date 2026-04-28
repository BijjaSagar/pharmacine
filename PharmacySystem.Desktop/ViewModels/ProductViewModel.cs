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
    public class ProductViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;

        public ObservableCollection<Product> Products { get; } = new ObservableCollection<Product>();

        private Product _selectedProduct = new Product();
        public Product SelectedProduct
        {
            get => _selectedProduct;
            set => SetProperty(ref _selectedProduct, value);
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

        public ProductViewModel()
        {
            _dbService = new DatabaseService();
            LoadCommand = new RelayCommand(async _ => await LoadProductsAsync());
            SaveCommand = new RelayCommand(async _ => await SaveProductAsync(), _ => !IsBusy);
            ClearCommand = new RelayCommand(_ => SelectedProduct = new Product());
            
            _ = LoadProductsAsync();
        }

        private async Task LoadProductsAsync()
        {
            IsBusy = true;
            try
            {
                var dt = await _dbService.ExecuteQueryAsync("SELECT * FROM products ORDER BY name LIMIT 500");
                Products.Clear();
                foreach (DataRow row in dt.Rows)
                {
                    Products.Add(new Product
                    {
                        ProductId = Convert.ToInt32(row["product_id"]),
                        Barcode = row["barcode"].ToString() ?? "",
                        Name = row["name"].ToString() ?? "",
                        GenericName = row["generic_name"]?.ToString() ?? "",
                        CategoryId = row["category_id"] != DBNull.Value ? Convert.ToInt32(row["category_id"]) : null,
                        PackSize = row["pack_size"]?.ToString() ?? "",
                        ReorderLevel = Convert.ToInt32(row["reorder_level"]),
                        UnitPrice = Convert.ToDecimal(row["unit_price"]),
                        GstPercent = Convert.ToDecimal(row["gst_percent"]),
                        IsPrescriptionRequired = Convert.ToBoolean(row["is_prescription_required"]),
                        IsScheduleH1 = row["is_schedule_h1"] != DBNull.Value && Convert.ToBoolean(row["is_schedule_h1"]),
                        ShelfLocation = row["shelf_location"]?.ToString() ?? "Store",
                        IsActive = Convert.ToBoolean(row["is_active"])
                    });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Loading Products");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task SaveProductAsync()
        {
            if (string.IsNullOrWhiteSpace(SelectedProduct.Name) || string.IsNullOrWhiteSpace(SelectedProduct.Barcode))
            {
                MessageBox.Show("Barcode and Name are required.", "Validation Error");
                return;
            }

            IsBusy = true;
            try
            {
                if (SelectedProduct.ProductId == 0)
                {
                    var sql = @"INSERT INTO products (barcode, name, generic_name, pack_size, reorder_level, unit_price, gst_percent, is_prescription_required, is_schedule_h1, shelf_location, is_active) 
                                VALUES (@bc, @name, @gn, @ps, @rl, @up, @gst, @pr, @h1, @sl, @ia)";
                    await _dbService.ExecuteNonQueryAsync(sql,
                        new NpgsqlParameter("@bc", SelectedProduct.Barcode),
                        new NpgsqlParameter("@name", SelectedProduct.Name),
                        new NpgsqlParameter("@gn", SelectedProduct.GenericName),
                        new NpgsqlParameter("@ps", SelectedProduct.PackSize),
                        new NpgsqlParameter("@rl", SelectedProduct.ReorderLevel),
                        new NpgsqlParameter("@up", SelectedProduct.UnitPrice),
                        new NpgsqlParameter("@gst", SelectedProduct.GstPercent),
                        new NpgsqlParameter("@pr", SelectedProduct.IsPrescriptionRequired),
                        new NpgsqlParameter("@h1", SelectedProduct.IsScheduleH1),
                        new NpgsqlParameter("@sl", SelectedProduct.ShelfLocation),
                        new NpgsqlParameter("@ia", SelectedProduct.IsActive));
                }
                else
                {
                    var sql = @"UPDATE products SET barcode=@bc, name=@name, generic_name=@gn, pack_size=@ps, reorder_level=@rl, 
                                unit_price=@up, gst_percent=@gst, is_prescription_required=@pr, is_schedule_h1=@h1, shelf_location=@sl, is_active=@ia, last_modified=CURRENT_TIMESTAMP 
                                WHERE product_id=@id";
                    await _dbService.ExecuteNonQueryAsync(sql,
                        new NpgsqlParameter("@id", SelectedProduct.ProductId),
                        new NpgsqlParameter("@bc", SelectedProduct.Barcode),
                        new NpgsqlParameter("@name", SelectedProduct.Name),
                        new NpgsqlParameter("@gn", SelectedProduct.GenericName),
                        new NpgsqlParameter("@ps", SelectedProduct.PackSize),
                        new NpgsqlParameter("@rl", SelectedProduct.ReorderLevel),
                        new NpgsqlParameter("@up", SelectedProduct.UnitPrice),
                        new NpgsqlParameter("@gst", SelectedProduct.GstPercent),
                        new NpgsqlParameter("@pr", SelectedProduct.IsPrescriptionRequired),
                        new NpgsqlParameter("@h1", SelectedProduct.IsScheduleH1),
                        new NpgsqlParameter("@sl", SelectedProduct.ShelfLocation),
                        new NpgsqlParameter("@ia", SelectedProduct.IsActive));
                }
                
                await LoadProductsAsync();
                SelectedProduct = new Product(); 
                MessageBox.Show("Product Saved Successfully!", "Success");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error Saving Product");
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
