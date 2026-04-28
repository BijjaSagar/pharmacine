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
        public bool IsScheduleH1 => Product.IsScheduleH1;

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
        public string GenericName { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsScheduleH1 { get; set; }
        public decimal Mrp { get; set; }
        public int BatchId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public decimal StockAvailable { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string ShelfLocation { get; set; } = string.Empty;
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

        private string _substituteName = string.Empty;
        public string SubstituteName { get => _substituteName; set => SetProperty(ref _substituteName, value); }

        private string _copilotQuery = string.Empty;
        public string CopilotQuery { get => _copilotQuery; set => SetProperty(ref _copilotQuery, value); }

        private string _copilotResponse = string.Empty;
        public string CopilotResponse { get => _copilotResponse; set => SetProperty(ref _copilotResponse, value); }

        private string _customerName = string.Empty;
        public string CustomerName { get => _customerName; set => SetProperty(ref _customerName, value); }

        private string _doctorName = string.Empty;
        public string DoctorName { get => _doctorName; set => SetProperty(ref _doctorName, value); }

        private string _customerPhone = string.Empty;
        public string CustomerPhone { get => _customerPhone; set => SetProperty(ref _customerPhone, value); }

        private string _patientAddress = string.Empty;
        public string PatientAddress { get => _patientAddress; set => SetProperty(ref _patientAddress, value); }

        private string _paymentMode = "Cash";
        public string PaymentMode { get => _paymentMode; set => SetProperty(ref _paymentMode, value); }
        public ObservableCollection<string> PaymentModes { get; } = new() { "Cash", "Card", "UPI", "Credit" };

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

        private bool _isChronicPatient;
        public bool IsChronicPatient { get => _isChronicPatient; set => SetProperty(ref _isChronicPatient, value); }

        private int _supplyDays = 30;
        public int SupplyDays { get => _supplyDays; set => SetProperty(ref _supplyDays, value); }

        private bool _sendWhatsAppReceipt = false;
        public bool SendWhatsAppReceipt { get => _sendWhatsAppReceipt; set => SetProperty(ref _sendWhatsAppReceipt, value); }

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
            PatientAddress = string.Empty;
            PaymentMode = "Cash";
            DiscountPercent = 0;
            IsChronicPatient = false;
            SupplyDays = 30;
            SendWhatsAppReceipt = false;
            RecalcTotals();
        }
    }

    /// <summary>
    /// Master POS ViewModel managing 10 consoles, search with alternatives, and checkout.
    /// </summary>
    public class SaleViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;
        private readonly AiService _aiService;

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

        private bool _isPinRequested;
        public bool IsPinRequested
        {
            get => _isPinRequested;
            set => SetProperty(ref _isPinRequested, value);
        }

        private string _enteredPin = string.Empty;
        public string EnteredPin
        {
            get => _enteredPin;
            set => SetProperty(ref _enteredPin, value);
        }

        private Action? _pendingAuthAction;

        // --- Commands ---
        public ICommand SwitchConsoleCommand { get; }
        public ICommand AddToCartCommand { get; }
        public ICommand RemoveFromCartCommand { get; }
        public ICommand CheckoutCommand { get; }
        public ICommand CancelBillCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }
        public ICommand SubstituteSearchCommand { get; }
        public ICommand AskCopilotCommand { get; }

        public ICommand ConfirmPinCommand { get; }
        public ICommand CancelPinCommand { get; }

        public SaleViewModel()
        {
            _dbService = new DatabaseService();
            _aiService = new AiService();

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
                    RequireAuthorization(() =>
                    {
                        ActiveConsole.CartItems.Remove(ci);
                        ActiveConsole.RecalcTotals();
                    });
                }
            });

            CheckoutCommand = new RelayCommand(async _ => await CheckoutAsync());
            CancelBillCommand = new RelayCommand(_ => 
            {
                if (ActiveConsole.CartItems.Count > 0)
                {
                    RequireAuthorization(() => ActiveConsole.Clear());
                }
                else
                {
                    ActiveConsole.Clear();
                }
            });
            SearchCommand = new RelayCommand(async _ => await SearchProductsAsync(false));
            SubstituteSearchCommand = new RelayCommand(async _ => await SearchProductsAsync(true));
            AskCopilotCommand = new RelayCommand(async _ => await AskCopilotAsync());
            ClearSearchCommand = new RelayCommand(_ =>
            {
                SearchInput = string.Empty;
                SearchResults.Clear();
                IsSearchVisible = false;
            });

            ConfirmPinCommand = new RelayCommand(async _ => await AuthorizePinAsync());
            CancelPinCommand = new RelayCommand(_ => 
            {
                IsPinRequested = false;
                EnteredPin = string.Empty;
                _pendingAuthAction = null;
            });
        }

        private void RequireAuthorization(Action action)
        {
            if (Helpers.AppSession.IsOwnerOrManager)
            {
                action.Invoke();
            }
            else
            {
                _pendingAuthAction = action;
                EnteredPin = string.Empty;
                IsPinRequested = true;
            }
        }

        private async Task AuthorizePinAsync()
        {
            if (string.IsNullOrWhiteSpace(EnteredPin)) return;

            // Verify pin against owners and managers
            var sql = "SELECT COUNT(*) FROM users WHERE role IN ('Owner', 'Manager') AND override_pin = @pin AND is_active = true";
            var count = Convert.ToInt32(await _dbService.ExecuteScalarAsync(sql, new NpgsqlParameter("@pin", EnteredPin)));

            if (count > 0)
            {
                IsPinRequested = false;
                EnteredPin = string.Empty;
                _pendingAuthAction?.Invoke();
                _pendingAuthAction = null;
            }
            else
            {
                StatusMessage = "Invalid Override PIN.";
                EnteredPin = string.Empty;
            }
        }

        private async Task SearchProductsAsync(bool substituteSearch = false)
        {
            if (string.IsNullOrWhiteSpace(SearchInput) || SearchInput.Length < 2)
            {
                SearchResults.Clear();
                IsSearchVisible = false;
                return;
            }

            try
            {
                string queryStr = SearchInput;
                if (substituteSearch)
                {
                    // Find the generic name for the currently typed brand
                    var genericSql = "SELECT generic_name FROM products WHERE name ILIKE @q OR barcode ILIKE @q LIMIT 1";
                    var genDt = await _dbService.ExecuteQueryAsync(genericSql, new NpgsqlParameter("@q", $"%{SearchInput}%"));
                    if (genDt.Rows.Count > 0)
                    {
                        var gn = genDt.Rows[0]["generic_name"].ToString();
                        if (!string.IsNullOrWhiteSpace(gn))
                        {
                            queryStr = gn; // Replace search string with the actual salt
                            StatusMessage = $"Showing substitutes for salt: {gn}";
                        }
                    }
                }

                // Search by name OR barcode; show stock and nearest expiry batch
                var sql = @"
                    SELECT p.product_id, p.name, p.generic_name, p.barcode, p.category, p.mrp, p.is_schedule_h1, p.shelf_location,
                           b.batch_id, b.batch_number, b.quantity, b.expiry_date
                    FROM products p
                    LEFT JOIN LATERAL (
                        SELECT batch_id, batch_number, quantity, expiry_date
                        FROM batches
                        WHERE product_id = p.product_id AND quantity > 0
                        ORDER BY expiry_date ASC LIMIT 1
                    ) b ON true
                    WHERE p.is_active = true
                      AND (p.name ILIKE @q OR p.barcode ILIKE @q OR p.category ILIKE @q OR (p.generic_name ILIKE @q AND @isSub = true))
                    ORDER BY (p.mrp - p.unit_price) DESC, p.name
                    LIMIT 20";

                var dt = await _dbService.ExecuteQueryAsync(sql,
                    new NpgsqlParameter("@q", $"%{queryStr}%"),
                    new NpgsqlParameter("@isSub", substituteSearch));

                SearchResults.Clear();
                foreach (System.Data.DataRow row in dt.Rows)
                {
                    SearchResults.Add(new SearchResult
                    {
                        ProductId = Convert.ToInt32(row["product_id"]),
                        Name = row["name"].ToString() ?? "",
                        GenericName = row["generic_name"].ToString() ?? "",
                        Barcode = row["barcode"].ToString() ?? "",
                        Category = row["category"].ToString() ?? "",
                        IsScheduleH1 = row["is_schedule_h1"] != DBNull.Value && Convert.ToBoolean(row["is_schedule_h1"]),
                        Mrp = row["mrp"] != DBNull.Value ? Convert.ToDecimal(row["mrp"]) : 0,
                        BatchId = row["batch_id"] != DBNull.Value ? Convert.ToInt32(row["batch_id"]) : 0,
                        BatchNumber = row["batch_number"]?.ToString() ?? "N/A",
                        StockAvailable = row["quantity"] != DBNull.Value ? Convert.ToDecimal(row["quantity"]) : 0,
                        ExpiryDate = row["expiry_date"] != DBNull.Value ? Convert.ToDateTime(row["expiry_date"]) : DateTime.MaxValue,
                        ShelfLocation = row["shelf_location"]?.ToString() ?? "Store"
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
                    GstPercent = row["gst_percent"] != DBNull.Value ? Convert.ToDecimal(row["gst_percent"]) : 0,
                    IsScheduleH1 = row["is_schedule_h1"] != DBNull.Value && Convert.ToBoolean(row["is_schedule_h1"]),
                    ShelfLocation = row["shelf_location"].ToString() ?? "Store"
                };

                // ── 2. FIFO ENFORCEMENT ──────────────────────────────────────────
                if (SelectedSearchResult.BatchId > 0 && SelectedSearchResult.ExpiryDate < DateTime.MaxValue)
                {
                    string fifoSql = @"
                        SELECT batch_number, expiry_date 
                        FROM batches 
                        WHERE product_id = @pid 
                          AND quantity > 0 
                          AND expiry_date >= CURRENT_DATE 
                          AND expiry_date < @exp 
                          AND batch_id != @bid 
                        ORDER BY expiry_date ASC 
                        LIMIT 1";

                    var olderDt = await _dbService.ExecuteQueryAsync(fifoSql,
                        new NpgsqlParameter("@pid", product.ProductId),
                        new NpgsqlParameter("@exp", SelectedSearchResult.ExpiryDate),
                        new NpgsqlParameter("@bid", SelectedSearchResult.BatchId));

                    if (olderDt.Rows.Count > 0)
                    {
                        var olderBatch = olderDt.Rows[0]["batch_number"].ToString();
                        StatusMessage = $"🚨 FIFO ALERT: Older batch ({olderBatch}) exists in stock! Sell that first.";
                        return; // Block adding to cart
                    }
                }

                // Add item
                var existing = ActiveConsole.CartItems.FirstOrDefault(x => x.BatchId == SelectedSearchResult.BatchId);
                if (existing != null)
                {
                    existing.Quantity += AddQuantity;
                }
                else
                {
                    var prodSql = "SELECT * FROM products WHERE product_id = @id";
                    var dt = await _dbService.ExecuteQueryAsync(prodSql, new NpgsqlParameter("@id", SelectedSearchResult.ProductId));
                    if (dt.Rows.Count > 0)
                    {
                        var p = new Product
                        {
                            ProductId = (int)dt.Rows[0]["product_id"],
                            Name = dt.Rows[0]["name"].ToString() ?? "",
                            GenericName = dt.Rows[0]["generic_name"].ToString() ?? "",
                            UnitPrice = Convert.ToDecimal(dt.Rows[0]["unit_price"]),
                            GstPercent = Convert.ToDecimal(dt.Rows[0]["gst_percent"]),
                            IsScheduleH1 = Convert.ToBoolean(dt.Rows[0]["is_schedule_h1"])
                        };

                        ActiveConsole.CartItems.Add(new CartItem
                        {
                            Product = p,
                            BatchId = SelectedSearchResult.BatchId,
                            BatchNumber = SelectedSearchResult.BatchNumber,
                            StockAvailable = SelectedSearchResult.StockAvailable,
                            Quantity = AddQuantity
                        });
                    }
                }

                ActiveConsole.RecalcTotals();
                StatusMessage = $"Added {AddQuantity} of {SelectedSearchResult.Name} to cart.";
                
                // AI Check Interactions in background
                if (_aiService.IsConfigured && ActiveConsole.CartItems.Count > 1)
                {
                    _ = Task.Run(async () => {
                        var salts = ActiveConsole.CartItems.Select(x => x.Product.GenericName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
                        if (salts.Count > 1) {
                            var interaction = await _aiService.CheckInteractionsAsync(salts);
                            if (interaction != null && interaction.HasInteraction) {
                                Avalonia.Threading.Dispatcher.UIThread.Post(() => {
                                    StatusMessage = $"🚨 AI WARNING: {interaction.WarningMessage}";
                                });
                            }
                        }
                    });
                }

                SearchInput = string.Empty;
                SearchResults.Clear();
                IsSearchVisible = false;
                AddQuantity = 1;
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
                // ── SCHEDULE H1 COMPLIANCE ──
                if (console.CartItems.Any(c => c.IsScheduleH1))
                {
                    if (string.IsNullOrWhiteSpace(console.DoctorName) || string.IsNullOrWhiteSpace(console.PatientAddress))
                    {
                        StatusMessage = "🚨 Schedule H1 Alert: Doctor Name and Patient Address are REQUIRED!";
                        IsBusy = false;
                        return;
                    }
                }

                int? customerId = null;

                // ── KHATA (CREDIT) LOGIC ──
                if (console.PaymentMode == "Credit")
                {
                    if (string.IsNullOrWhiteSpace(console.CustomerPhone) || string.IsNullOrWhiteSpace(console.CustomerName))
                    {
                        StatusMessage = "🚨 Credit Alert: Customer Name and Phone are REQUIRED for Khata!";
                        IsBusy = false;
                        return;
                    }

                    // Upsert Customer
                    string custSql = @"
                        INSERT INTO customers (name, mobile, address, outstanding_balance)
                        VALUES (@name, @phone, @addr, @bal)
                        ON CONFLICT (mobile) DO UPDATE SET 
                            outstanding_balance = customers.outstanding_balance + @bal,
                            name = @name
                        RETURNING customer_id";
                    var cidObj = await _dbService.ExecuteScalarAsync(custSql,
                        new NpgsqlParameter("@name", console.CustomerName),
                        new NpgsqlParameter("@phone", console.CustomerPhone),
                        new NpgsqlParameter("@addr", string.IsNullOrWhiteSpace(console.PatientAddress) ? DBNull.Value : console.PatientAddress),
                        new NpgsqlParameter("@bal", console.GrandTotal));
                    
                    customerId = Convert.ToInt32(cidObj);
                }

                string invoiceNo = $"INV-C{console.ConsoleNumber}-{DateTime.Now:yyMMddHHmmss}";

                string saleSql = @"INSERT INTO sales 
                    (invoice_no, customer_id, customer_name, customer_phone, doctor_name, patient_address,
                     subtotal, taxable_amount, total_gst, cgst, sgst, discount_amount, grand_total, total_amount, payment_mode, user_id, is_chronic, refill_due_date) 
                    VALUES (@inv, @cid, @cust, @phone, @doc, @addr, @sub, @sub, @gst, @gst/2, @gst/2, @disc, @grand, @grand, @pmode, @uid, @chronic, @refill) 
                    RETURNING sale_id";

                var saleIdObj = await _dbService.ExecuteScalarAsync(saleSql,
                    new NpgsqlParameter("@inv", invoiceNo),
                    new NpgsqlParameter("@cid", (object?)customerId ?? DBNull.Value),
                    new NpgsqlParameter("@cust", string.IsNullOrEmpty(console.CustomerName) ? "Walk-in" : console.CustomerName),
                    new NpgsqlParameter("@phone", (object?)console.CustomerPhone ?? DBNull.Value),
                    new NpgsqlParameter("@doc", (object?)console.DoctorName ?? DBNull.Value),
                    new NpgsqlParameter("@addr", (object?)console.PatientAddress ?? DBNull.Value),
                    new NpgsqlParameter("@sub", console.SubTotal),
                    new NpgsqlParameter("@gst", console.TotalGst),
                    new NpgsqlParameter("@disc", console.DiscountAmount),
                    new NpgsqlParameter("@grand", console.GrandTotal),
                    new NpgsqlParameter("@pmode", console.PaymentMode),
                    new NpgsqlParameter("@uid", AppSession.UserId),
                    new NpgsqlParameter("@chronic", console.IsChronicPatient),
                    new NpgsqlParameter("@refill", console.IsChronicPatient ? (object)DateTime.Now.Date.AddDays(console.SupplyDays - 2) : DBNull.Value));

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

                if (console.PaymentMode == "Credit" && customerId.HasValue)
                {
                    string ledgerSql = @"
                        INSERT INTO customer_ledger (customer_id, sale_id, transaction_type, amount, notes)
                        VALUES (@cid, @sid, 'CREDIT', @amt, 'Credit Sale')";
                    await _dbService.ExecuteNonQueryAsync(ledgerSql,
                        new NpgsqlParameter("@cid", customerId.Value),
                        new NpgsqlParameter("@sid", saleId),
                        new NpgsqlParameter("@amt", console.GrandTotal));
                }

                if (console.SendWhatsAppReceipt && !string.IsNullOrWhiteSpace(console.CustomerPhone))
                {
                    string message = $"Hello {console.CustomerName},\nThank you for visiting ClinicOS Pharmacy!\nYour Bill {invoiceNo} for Rs.{console.GrandTotal} has been paid.\n\nStay Healthy!";
                    string url = $"https://wa.me/91{console.CustomerPhone}?text={Uri.EscapeDataString(message)}";
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = url,
                        UseShellExecute = true
                    });
                }
                else
                {
                    // Trigger actual ESC/POS thermal printing
                    ThermalPrinterService.PrintReceipt(console, invoiceNo);
                }
                
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

        public async Task ProcessPrescriptionImageAsync(string filePath)
        {
            if (!_aiService.IsConfigured)
            {
                StatusMessage = "AI is not configured. Add GeminiApiKey in appsettings.";
                return;
            }

            try
            {
                IsBusy = true;
                StatusMessage = "AI is reading prescription...";
                var items = await _aiService.ReadPrescriptionAsync(filePath);
                
                if (items == null || items.Count == 0)
                {
                    StatusMessage = "AI could not extract medicines from the image.";
                    return;
                }

                int added = 0;
                foreach(var item in items)
                {
                    // Search DB for medicine
                    var sql = @"
                    SELECT p.product_id, p.name, p.generic_name, p.unit_price, p.gst_percent, p.is_schedule_h1,
                           b.batch_id, b.batch_number, b.quantity, b.expiry_date
                    FROM products p
                    LEFT JOIN LATERAL (
                        SELECT batch_id, batch_number, quantity, expiry_date
                        FROM batches
                        WHERE product_id = p.product_id AND quantity > 0
                        ORDER BY expiry_date ASC LIMIT 1
                    ) b ON true
                    WHERE p.is_active = true AND (p.name ILIKE @q OR p.generic_name ILIKE @q)
                    ORDER BY p.name LIMIT 1";

                    var dt = await _dbService.ExecuteQueryAsync(sql, new NpgsqlParameter("@q", $"%{item.MedicineName}%"));
                    if (dt.Rows.Count > 0)
                    {
                        var p = new Product
                        {
                            ProductId = (int)dt.Rows[0]["product_id"],
                            Name = dt.Rows[0]["name"].ToString() ?? "",
                            GenericName = dt.Rows[0]["generic_name"].ToString() ?? "",
                            UnitPrice = Convert.ToDecimal(dt.Rows[0]["unit_price"]),
                            GstPercent = Convert.ToDecimal(dt.Rows[0]["gst_percent"]),
                            IsScheduleH1 = Convert.ToBoolean(dt.Rows[0]["is_schedule_h1"])
                        };

                        var bidObj = dt.Rows[0]["batch_id"];
                        int bid = bidObj == DBNull.Value ? 0 : Convert.ToInt32(bidObj);
                        decimal stock = bidObj == DBNull.Value ? 0 : Convert.ToDecimal(dt.Rows[0]["quantity"]);

                        ActiveConsole.CartItems.Add(new CartItem
                        {
                            Product = p,
                            BatchId = bid,
                            BatchNumber = dt.Rows[0]["batch_number"]?.ToString() ?? "",
                            StockAvailable = stock,
                            Quantity = item.Quantity > 0 ? item.Quantity : 1
                        });
                        added++;
                    }
                }
                
                ActiveConsole.RecalcTotals();
                StatusMessage = $"AI recognized and added {added} medicines.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"AI Vision Error: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task AskCopilotAsync()
        {
            if (ActiveConsole == null || string.IsNullOrWhiteSpace(ActiveConsole.CopilotQuery)) return;
            if (!_aiService.IsConfigured) { ActiveConsole.CopilotResponse = "AI Key not configured."; return; }
            ActiveConsole.CopilotResponse = "Thinking...";
            try
            {
                ActiveConsole.CopilotResponse = await _aiService.GenerateContentAsync("You are a helpful pharmacist AI assistant. " + ActiveConsole.CopilotQuery);
            }
            catch (Exception ex)
            {
                ActiveConsole.CopilotResponse = "Error: " + ex.Message;
            }
        }
    }
}
