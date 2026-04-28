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
    public class PurchaseItem : ViewModelBase
    {
        public Product Product { get; set; } = new();
        public string BatchNumber { get; set; } = string.Empty;
        
        private decimal _quantity = 1;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                SetProperty(ref _quantity, value);
                OnPropertyChanged(nameof(TotalCost));
            }
        }

        private decimal _costPrice;
        public decimal CostPrice
        {
            get => _costPrice;
            set
            {
                SetProperty(ref _costPrice, value);
                OnPropertyChanged(nameof(TotalCost));
            }
        }

        private decimal _mrp;
        public decimal Mrp
        {
            get => _mrp;
            set => SetProperty(ref _mrp, value);
        }

        private DateTime _expiryDate = DateTime.Now.AddYears(1);
        public DateTime ExpiryDate
        {
            get => _expiryDate;
            set => SetProperty(ref _expiryDate, value);
        }

        public decimal TotalCost => CostPrice * Quantity;
    }

    public class PurchaseViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;

        public ObservableCollection<Supplier> Suppliers { get; set; } = new();
        public ObservableCollection<PurchaseItem> PurchaseItems { get; set; } = new();

        private Supplier _selectedSupplier;
        public Supplier SelectedSupplier
        {
            get => _selectedSupplier;
            set => SetProperty(ref _selectedSupplier, value);
        }

        private string _invoiceNo = string.Empty;
        public string InvoiceNo
        {
            get => _invoiceNo;
            set => SetProperty(ref _invoiceNo, value);
        }

        private string _barcodeInput = string.Empty;
        public string BarcodeInput
        {
            get => _barcodeInput;
            set => SetProperty(ref _barcodeInput, value);
        }

        private decimal _grandTotal;
        public decimal GrandTotal
        {
            get => _grandTotal;
            set => SetProperty(ref _grandTotal, value);
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

        public ICommand ScanCommand { get; }
        public ICommand SavePurchaseCommand { get; }

        public PurchaseViewModel()
        {
            _dbService = new DatabaseService();
            ScanCommand = new RelayCommand(async _ => await ProcessBarcodeAsync());
            SavePurchaseCommand = new RelayCommand(async _ => await SavePurchaseAsync());
            _ = LoadSuppliersAsync();
        }

        private async Task LoadSuppliersAsync()
        {
            try
            {
                var dt = await _dbService.ExecuteQueryAsync("SELECT * FROM suppliers WHERE is_active = true");
                foreach (System.Data.DataRow row in dt.Rows)
                {
                    Suppliers.Add(new Supplier
                    {
                        SupplierId = Convert.ToInt32(row["supplier_id"]),
                        Name = row["name"].ToString() ?? ""
                    });
                }
                SelectedSupplier = Suppliers.FirstOrDefault();
            }
            catch (Exception ex)
            {
                ErrorMessage = "Failed to load suppliers: " + ex.Message;
            }
        }

        private async Task ProcessBarcodeAsync()
        {
            if (string.IsNullOrWhiteSpace(BarcodeInput)) return;
            ErrorMessage = string.Empty;

            try
            {
                var sql = "SELECT * FROM products WHERE barcode = @bc AND is_active = true LIMIT 1";
                var dt = await _dbService.ExecuteQueryAsync(sql, new NpgsqlParameter("@bc", BarcodeInput));

                if (dt.Rows.Count > 0)
                {
                    var row = dt.Rows[0];
                    var product = new Product
                    {
                        ProductId = Convert.ToInt32(row["product_id"]),
                        Barcode = row["barcode"].ToString() ?? "",
                        Name = row["name"].ToString() ?? "",
                        UnitPrice = Convert.ToDecimal(row["unit_price"])
                    };

                    var newItem = new PurchaseItem 
                    { 
                        Product = product, 
                        CostPrice = product.UnitPrice * 0.8M, // default 20% margin
                        Mrp = product.UnitPrice,
                        BatchNumber = "B-" + DateTime.Now.ToString("MMddHHmm")
                    };
                    
                    newItem.PropertyChanged += (s, e) => CalculateTotals();
                    PurchaseItems.Add(newItem);
                    CalculateTotals();
                }
                else
                {
                    ErrorMessage = "Product not found.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "DB Error: " + ex.Message;
            }

            BarcodeInput = string.Empty;
        }

        private void CalculateTotals()
        {
            GrandTotal = PurchaseItems.Sum(x => x.TotalCost);
        }

        private async Task SavePurchaseAsync()
        {
            if (!PurchaseItems.Any() || SelectedSupplier == null || string.IsNullOrWhiteSpace(InvoiceNo))
            {
                ErrorMessage = "Please fill invoice number, select supplier, and add items.";
                return;
            }
            
            IsBusy = true;
            try
            {
                // Task 3.2: Insert Purchase, Purchase Items, and update/insert Batches
                string purchaseSql = @"INSERT INTO purchases (supplier_id, invoice_no, purchase_date, total_cost) 
                                       VALUES (@sid, @inv, CURRENT_DATE, @total) RETURNING purchase_id";
                
                var purchaseIdObj = await _dbService.ExecuteScalarAsync(purchaseSql, 
                    new NpgsqlParameter("@sid", SelectedSupplier.SupplierId),
                    new NpgsqlParameter("@inv", InvoiceNo),
                    new NpgsqlParameter("@total", GrandTotal));
                    
                int purchaseId = Convert.ToInt32(purchaseIdObj);
                
                foreach (var item in PurchaseItems)
                {
                    // Create or update batch
                    string batchSql = @"INSERT INTO batches (product_id, batch_number, expiry_date, quantity, cost_price, mrp)
                                        VALUES (@pid, @bno, @exp, @qty, @cost, @mrp)
                                        ON CONFLICT (product_id, batch_number) DO UPDATE 
                                        SET quantity = batches.quantity + EXCLUDED.quantity RETURNING batch_id";
                                        
                    var batchIdObj = await _dbService.ExecuteScalarAsync(batchSql,
                        new NpgsqlParameter("@pid", item.Product.ProductId),
                        new NpgsqlParameter("@bno", item.BatchNumber),
                        new NpgsqlParameter("@exp", item.ExpiryDate),
                        new NpgsqlParameter("@qty", item.Quantity),
                        new NpgsqlParameter("@cost", item.CostPrice),
                        new NpgsqlParameter("@mrp", item.Mrp));
                        
                    int batchId = Convert.ToInt32(batchIdObj);
                    
                    // Add purchase item
                    string itemSql = @"INSERT INTO purchase_items (purchase_id, product_id, batch_id, quantity, cost_price, mrp, expiry_date)
                                       VALUES (@purId, @prodId, @batchId, @qty, @cost, @mrp, @exp)";
                                       
                    await _dbService.ExecuteNonQueryAsync(itemSql,
                        new NpgsqlParameter("@purId", purchaseId),
                        new NpgsqlParameter("@prodId", item.Product.ProductId),
                        new NpgsqlParameter("@batchId", batchId),
                        new NpgsqlParameter("@qty", item.Quantity),
                        new NpgsqlParameter("@cost", item.CostPrice),
                        new NpgsqlParameter("@mrp", item.Mrp),
                        new NpgsqlParameter("@exp", item.ExpiryDate));
                }
                
                PurchaseItems.Clear();
                CalculateTotals();
                InvoiceNo = string.Empty;
                System.Windows.MessageBox.Show($"Purchase saved successfully!", "Success");
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
    }
}
