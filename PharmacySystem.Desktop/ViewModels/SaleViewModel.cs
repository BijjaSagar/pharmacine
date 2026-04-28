using PharmacySystem.Desktop.Helpers;
using PharmacySystem.Desktop.Models;
using PharmacySystem.Desktop.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Npgsql;

namespace PharmacySystem.Desktop.ViewModels
{
    /// <summary>
    /// A single item row in the billing cart. Supports decimal quantities (e.g. 0.5 strip).
    /// </summary>
    public class CartItem : ViewModelBase
    {
        public Product Product { get; set; } = new();
        public int BatchId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public decimal StockAvailable { get; set; }

        private decimal _quantity = 1;
        public decimal Quantity
        {
            get => _quantity;
            set
            {
                SetProperty(ref _quantity, value);
                OnPropertyChanged(nameof(Total));
                OnPropertyChanged(nameof(GstAmount));
            }
        }

        public decimal GstAmount => (Product.UnitPrice * Product.GstPercent / 100M) * Quantity;
        public decimal Total => (Product.UnitPrice * Quantity) + GstAmount;
    }

    /// <summary>
    /// A search result row showing a product + its alternative brands.
    /// </summary>
    public class SearchResult
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal Mrp { get; set; }
        public int BatchId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public decimal StockAvailable { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string StockDisplay => $"Stock: {StockAvailable}  |  Exp: {ExpiryDate:MMM-yy}";
        public string PriceDisplay => $"₹ {Mrp:F2}";
    }

    /// <summary>
    /// One billing console state (customer name, doctor, cart, totals).
    /// </summary>
    public class ConsoleState : ViewModelBase
    {
        public int ConsoleNumber { get; }
        public string Header => $"Console {ConsoleNumber}";
        public string ShortcutHint => $"Ctrl+{ConsoleNumber}";

        private string _customerName = string.Empty;
        public string CustomerName { get => _customerName; set => SetProperty(ref _customerName, value); }

        private string _doctorName = string.Empty;
        public string DoctorName { get => _doctorName; set => SetProperty(ref _doctorName, value); }

        private string _customerPhone = string.Empty;
        public string CustomerPhone { get => _customerPhone; set => SetProperty(ref _customerPhone, value); }

        public ObservableCollection<CartItem> CartItems { get; set; } = new();

        private decimal _subTotal;
        public decimal SubTotal { get => _subTotal; set => SetProperty(ref _subTotal, value); }

        private decimal _totalGst;
        public decimal TotalGst { get => _totalGst; set => SetProperty(ref _totalGst, value); }

        private decimal _grandTotal;
        public decimal GrandTotal { get => _grandTotal; set => SetProperty(ref _grandTotal, value); }

        private decimal _discountPercent;
        public decimal DiscountPercent
        {
            get => _discountPercent;
            set
            {
                SetProperty(ref _discountPercent, value);
                RecalcTotals();
            }
        }

        private decimal _discountAmount;
        public decimal DiscountAmount { get => _discountAmount; set => SetProperty(ref _discountAmount, value); }

        private int _itemCount;
        public int ItemCount { get => _itemCount; set => SetProperty(ref _itemCount, value); }

        public ConsoleState(int number)
        {
            ConsoleNumber = number;
        }

        public void RecalcTotals()
        {
            SubTotal = CartItems.Sum(x => x.Product.UnitPrice * x.Quantity);
            TotalGst = CartItems.Sum(x => x.GstAmount);
            DiscountAmount = SubTotal * DiscountPercent / 100M;
            GrandTotal = SubTotal + TotalGst - DiscountAmount;
            ItemCount = CartItems.Count;
        }

        public void Clear()
        {
            CartItems.Clear();
            CustomerName = string.Empty;
            DoctorName = string.Empty;
            CustomerPhone = string.Empty;
            DiscountPercent = 0;
            RecalcTotals();
        }
    }

    /// <summary>
    /// Master POS ViewModel managing 10 consoles, search with alternatives, and checkout.
    /// </summary>
    public class SaleViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;

        // --- 10 Consoles ---
        public ObservableCollection<ConsoleState> Consoles { get; } = new();

        private ConsoleState _activeConsole;
        public ConsoleState ActiveConsole
        {
            get => _activeConsole;
            set => SetProperty(ref _activeConsole, value);
        }

        // --- Search ---
        private string _searchInput = string.Empty;
        public string SearchInput
        {
            get => _searchInput;
            set
            {
                SetProperty(ref _searchInput, value);
                _ = SearchProductsAsync();
            }
        }

        public ObservableCollection<SearchResult> SearchResults { get; } = new();

        private SearchResult? _selectedSearchResult;
        public SearchResult? SelectedSearchResult
        {
            get => _selectedSearchResult;
            set => SetProperty(ref _selectedSearchResult, value);
        }

        // --- Quick Add Controls ---
        private decimal _addQuantity = 1;
        public decimal AddQuantity
        {
            get => _addQuantity;
            set => SetProperty(ref _addQuantity, value);
        }

        private bool _isSearchVisible;
        public bool IsSearchVisible
        {
            get => _isSearchVisible;
            set => SetProperty(ref _isSearchVisible, value);
        }

        // --- Status / Error ---
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

        // --- Commands ---
        public ICommand SwitchConsoleCommand { get; }
        public ICommand AddToCartCommand { get; }
        public ICommand RemoveFromCartCommand { get; }
        public ICommand CheckoutCommand { get; }
        public ICommand CancelBillCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }

        public SaleViewModel()
        {
            _dbService = new DatabaseService();

            // Initialize 10 consoles
            for (int i = 1; i <= 10; i++)
                Consoles.Add(new ConsoleState(i));

            _activeConsole = Consoles[0];

            SwitchConsoleCommand = new RelayCommand(n =>
            {
                if (n is int num && num >= 1 && num <= 10)
                    ActiveConsole = Consoles[num - 1];
            });

            AddToCartCommand = new RelayCommand(async _ => await AddSelectedToCartAsync());
            RemoveFromCartCommand = new RelayCommand(item =>
            {
                if (item is CartItem ci)
                {
                    ActiveConsole.CartItems.Remove(ci);
                    ActiveConsole.RecalcTotals();
                }
            });

            CheckoutCommand = new RelayCommand(async _ => await CheckoutAsync());
            CancelBillCommand = new RelayCommand(_ => ActiveConsole.Clear());
            SearchCommand = new RelayCommand(async _ => await SearchProductsAsync());
            ClearSearchCommand = new RelayCommand(_ =>
            {
                SearchInput = string.Empty;
                SearchResults.Clear();
                IsSearchVisible = false;
            });
        }

        private async Task SearchProductsAsync()
        {
            if (string.IsNullOrWhiteSpace(SearchInput) || SearchInput.Length < 2)
            {
                SearchResults.Clear();
                IsSearchVisible = false;
                return;
            }

            try
            {
                // Search by name OR barcode; show stock and nearest expiry batch
                var sql = @"
                    SELECT p.product_id, p.name, p.barcode, p.category, p.mrp,
                           b.batch_id, b.batch_number, b.quantity, b.expiry_date
                    FROM products p
                    LEFT JOIN LATERAL (
                        SELECT batch_id, batch_number, quantity, expiry_date
                        FROM batches
                        WHERE product_id = p.product_id AND quantity > 0
                        ORDER BY expiry_date ASC LIMIT 1
                    ) b ON true
                    WHERE p.is_active = true
                      AND (p.name ILIKE @q OR p.barcode ILIKE @q OR p.category ILIKE @q)
                    ORDER BY p.name
                    LIMIT 20";

                var dt = await _dbService.ExecuteQueryAsync(sql,
                    new NpgsqlParameter("@q", $"%{SearchInput}%"));

                SearchResults.Clear();
                foreach (System.Data.DataRow row in dt.Rows)
                {
                    SearchResults.Add(new SearchResult
                    {
                        ProductId = Convert.ToInt32(row["product_id"]),
                        Name = row["name"].ToString() ?? "",
                        Barcode = row["barcode"].ToString() ?? "",
                        Category = row["category"].ToString() ?? "",
                        Mrp = row["mrp"] != DBNull.Value ? Convert.ToDecimal(row["mrp"]) : 0,
                        BatchId = row["batch_id"] != DBNull.Value ? Convert.ToInt32(row["batch_id"]) : 0,
                        BatchNumber = row["batch_number"]?.ToString() ?? "N/A",
                        StockAvailable = row["quantity"] != DBNull.Value ? Convert.ToDecimal(row["quantity"]) : 0,
                        ExpiryDate = row["expiry_date"] != DBNull.Value ? Convert.ToDateTime(row["expiry_date"]) : DateTime.MaxValue
                    });
                }

                IsSearchVisible = SearchResults.Count > 0;
                if (SearchResults.Count == 1)
                    SelectedSearchResult = SearchResults[0];
            }
            catch (Exception ex)
            {
                StatusMessage = "Search error: " + ex.Message;
            }
        }

        private async Task AddSelectedToCartAsync()
        {
            if (SelectedSearchResult == null) return;
            if (AddQuantity <= 0)
            {
                StatusMessage = "Quantity must be greater than 0.";
                return;
            }

            try
            {
                // Load full product details
                var sql = "SELECT * FROM products WHERE product_id = @pid";
                var dt = await _dbService.ExecuteQueryAsync(sql,
                    new NpgsqlParameter("@pid", SelectedSearchResult.ProductId));

                if (dt.Rows.Count == 0) return;
                var row = dt.Rows[0];

                var product = new Product
                {
                    ProductId = Convert.ToInt32(row["product_id"]),
                    Barcode = row["barcode"].ToString() ?? "",
                    Name = row["name"].ToString() ?? "",
                    UnitPrice = row["unit_price"] != DBNull.Value ? Convert.ToDecimal(row["unit_price"]) : SelectedSearchResult.Mrp,
                    GstPercent = row["gst_percent"] != DBNull.Value ? Convert.ToDecimal(row["gst_percent"]) : 0
                };

                var sr = SelectedSearchResult;
                var existing = ActiveConsole.CartItems
                    .FirstOrDefault(c => c.Product.ProductId == product.ProductId && c.BatchId == sr.BatchId);

                if (existing != null)
                {
                    existing.Quantity += AddQuantity;
                }
                else
                {
                    var newItem = new CartItem
                    {
                        Product = product,
                        BatchId = sr.BatchId,
                        BatchNumber = sr.BatchNumber,
                        StockAvailable = sr.StockAvailable,
                        Quantity = AddQuantity
                    };
                    newItem.PropertyChanged += (s, e) => ActiveConsole.RecalcTotals();
                    ActiveConsole.CartItems.Add(newItem);
                }

                ActiveConsole.RecalcTotals();

                // Reset
                AddQuantity = 1;
                SearchInput = string.Empty;
                SearchResults.Clear();
                IsSearchVisible = false;
                StatusMessage = $"Added: {product.Name} x {AddQuantity}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Add to cart error: " + ex.Message;
            }
        }

        private async Task CheckoutAsync()
        {
            var console = ActiveConsole;
            if (!console.CartItems.Any())
            {
                StatusMessage = "Cart is empty!";
                return;
            }

            IsBusy = true;
            try
            {
                string invoiceNo = $"INV-C{console.ConsoleNumber}-{DateTime.Now:yyMMddHHmmss}";

                string saleSql = @"INSERT INTO sales 
                    (invoice_number, customer_name, customer_phone, doctor_name, 
                     subtotal, total_gst, discount_amount, total_amount, payment_mode, user_id) 
                    VALUES (@inv, @cust, @phone, @doc, @sub, @gst, @disc, @grand, 'Cash', @uid) 
                    RETURNING sale_id";

                var saleIdObj = await _dbService.ExecuteScalarAsync(saleSql,
                    new NpgsqlParameter("@inv", invoiceNo),
                    new NpgsqlParameter("@cust", string.IsNullOrEmpty(console.CustomerName) ? "Walk-in" : console.CustomerName),
                    new NpgsqlParameter("@phone", (object?)console.CustomerPhone ?? DBNull.Value),
                    new NpgsqlParameter("@doc", (object?)console.DoctorName ?? DBNull.Value),
                    new NpgsqlParameter("@sub", console.SubTotal),
                    new NpgsqlParameter("@gst", console.TotalGst),
                    new NpgsqlParameter("@disc", console.DiscountAmount),
                    new NpgsqlParameter("@grand", console.GrandTotal),
                    new NpgsqlParameter("@uid", AppSession.UserId));

                int saleId = Convert.ToInt32(saleIdObj);

                foreach (var item in console.CartItems)
                {
                    string itemSql = @"INSERT INTO sale_items (sale_id, batch_id, quantity, selling_price, subtotal)
                                       VALUES (@sid, @bid, @qty, @price, @subtotal)";
                    await _dbService.ExecuteNonQueryAsync(itemSql,
                        new NpgsqlParameter("@sid", saleId),
                        new NpgsqlParameter("@bid", item.BatchId > 0 ? item.BatchId : (object)DBNull.Value),
                        new NpgsqlParameter("@qty", (double)item.Quantity),
                        new NpgsqlParameter("@price", item.Product.UnitPrice),
                        new NpgsqlParameter("@subtotal", item.Total));

                    if (item.BatchId > 0)
                    {
                        await _dbService.ExecuteNonQueryAsync(
                            "UPDATE batches SET quantity = quantity - @qty WHERE batch_id = @bid",
                            new NpgsqlParameter("@qty", (double)item.Quantity),
                            new NpgsqlParameter("@bid", item.BatchId));
                    }
                }

                PrintReceipt(console, invoiceNo);
                console.Clear();
                StatusMessage = $"✓ Bill #{invoiceNo} saved! Console {console.ConsoleNumber} cleared.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Checkout failed: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void PrintReceipt(ConsoleState console, string invoiceNo)
        {
            Console.WriteLine($"=== PHARMACY RECEIPT === Console {console.ConsoleNumber}");
            Console.WriteLine($"Invoice: {invoiceNo}");
            Console.WriteLine($"Customer: {console.CustomerName}  |  Dr: {console.DoctorName}");
            Console.WriteLine($"Total: ₹{console.GrandTotal:F2}");
            Console.WriteLine("========================");
        }
    }
}
