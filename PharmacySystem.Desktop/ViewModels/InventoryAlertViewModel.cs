using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using Npgsql;
using PharmacySystem.Desktop.Helpers;
using PharmacySystem.Desktop.Services;

namespace PharmacySystem.Desktop.ViewModels
{
    // ── Data rows displayed in each alert list ──────────────────────────────
    public class StockAlertItem
    {
        public string MedicineName  { get; set; } = string.Empty;
        public string BatchNumber   { get; set; } = string.Empty;
        public decimal StockQty     { get; set; }
        public decimal ReorderLevel { get; set; }
        public string Supplier      { get; set; } = string.Empty;
        public string Category      { get; set; } = string.Empty;
        public string StockDisplay  => $"Stock: {StockQty:N2}  /  Reorder: {ReorderLevel:N0}";
        public string StatusTag     => StockQty <= 0 ? "OUT" : "LOW";
        public string StatusColor   => StockQty <= 0 ? "#EF4444" : "#F59E0B";
    }

    public class ExpiryAlertItem
    {
        public string MedicineName { get; set; } = string.Empty;
        public string BatchNumber  { get; set; } = string.Empty;
        public decimal StockQty    { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int DaysLeft        { get; set; }
        public string Supplier     { get; set; } = string.Empty;
        public string ExpiryDisplay => ExpiryDate.ToString("dd-MMM-yyyy");
        public string DaysDisplay   => DaysLeft <= 0 ? "EXPIRED" : $"{DaysLeft}d left";
        public string StatusColor   => DaysLeft <= 0 ? "#EF4444" : DaysLeft <= 15 ? "#F97316" : "#F59E0B";
    }

    public class DeadStockItem
    {
        public string MedicineName  { get; set; } = string.Empty;
        public string BatchNumber   { get; set; } = string.Empty;
        public decimal StockQty     { get; set; }
        public DateTime LastMovement { get; set; }
        public int IdleDays          { get; set; }
        public string IdleDisplay    => $"{IdleDays} days idle";
    }

    // ── ViewModel ──────────────────────────────────────────────────────────
    public class InventoryAlertViewModel : ViewModelBase
    {
        private readonly DatabaseService _db;

        // Collections
        public ObservableCollection<StockAlertItem>  LowStockItems  { get; } = new();
        public ObservableCollection<StockAlertItem>  OutOfStockItems { get; } = new();
        public ObservableCollection<ExpiryAlertItem> ExpiryItems     { get; } = new();
        public ObservableCollection<DeadStockItem>   DeadStockItems  { get; } = new();

        // Summary counts
        private int _lowStockCount;
        public int LowStockCount  { get => _lowStockCount;  set => SetProperty(ref _lowStockCount, value); }

        private int _outOfStockCount;
        public int OutOfStockCount { get => _outOfStockCount; set => SetProperty(ref _outOfStockCount, value); }

        private int _expiringCount;
        public int ExpiringCount  { get => _expiringCount;  set => SetProperty(ref _expiringCount, value); }

        private int _expiredCount;
        public int ExpiredCount   { get => _expiredCount;   set => SetProperty(ref _expiredCount, value); }

        private int _deadStockCount;
        public int DeadStockCount { get => _deadStockCount; set => SetProperty(ref _deadStockCount, value); }

        // Expiry filter (days)
        private int _expiryDays = 30;
        public int ExpiryDays
        {
            get => _expiryDays;
            set { SetProperty(ref _expiryDays, value); _ = LoadExpiryAsync(); }
        }

        // Idle threshold
        private int _idleDays = 90;
        public int IdleDays
        {
            get => _idleDays;
            set { SetProperty(ref _idleDays, value); _ = LoadDeadStockAsync(); }
        }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        private string _lastRefreshed = "Never";
        public string LastRefreshed { get => _lastRefreshed; set => SetProperty(ref _lastRefreshed, value); }

        public ICommand RefreshCommand { get; }
        public ICommand Set15DaysCommand { get; }
        public ICommand Set30DaysCommand { get; }
        public ICommand Set60DaysCommand { get; }

        public InventoryAlertViewModel()
        {
            _db = new DatabaseService();
            RefreshCommand    = new RelayCommand(async _ => await LoadAllAsync());
            Set15DaysCommand  = new RelayCommand(_ => ExpiryDays = 15);
            Set30DaysCommand  = new RelayCommand(_ => ExpiryDays = 30);
            Set60DaysCommand  = new RelayCommand(_ => ExpiryDays = 60);
            _ = LoadAllAsync();
        }

        // ── Master refresh ────────────────────────────────────────────────
        public async Task LoadAllAsync()
        {
            IsBusy = true;
            try
            {
                await Task.WhenAll(
                    LoadLowStockAsync(),
                    LoadExpiryAsync(),
                    LoadDeadStockAsync()
                );
                LastRefreshed = $"Last refreshed: {DateTime.Now:hh:mm:ss tt}";
            }
            finally { IsBusy = false; }
        }

        // ── 1. Low Stock & Out-of-Stock ───────────────────────────────────
        private async Task LoadLowStockAsync()
        {
            LowStockItems.Clear();
            OutOfStockItems.Clear();

            const string sql = @"
                SELECT
                    p.name            AS medicine,
                    b.batch_number,
                    b.quantity        AS stock,
                    p.reorder_level,
                    COALESCE(s.name,'—') AS supplier,
                    COALESCE(c.name,'—') AS category
                FROM products p
                LEFT JOIN LATERAL (
                    SELECT batch_number, quantity
                    FROM   batches
                    WHERE  product_id = p.product_id
                    ORDER  BY quantity ASC
                    LIMIT  1
                ) b ON true
                LEFT JOIN suppliers s ON s.supplier_id = (
                    SELECT pi.supplier_id FROM purchases pi
                    JOIN purchase_items pii ON pii.purchase_id = pi.purchase_id
                    WHERE pii.product_id = p.product_id
                    ORDER BY pi.purchase_date DESC LIMIT 1
                )
                LEFT JOIN categories c ON c.category_id = p.category_id
                WHERE p.is_active = true
                  AND (
                        (b.quantity <= p.reorder_level AND p.reorder_level > 0)
                     OR  b.quantity = 0
                     OR  b.quantity IS NULL
                  )
                ORDER BY b.quantity ASC NULLS FIRST
                LIMIT 100";

            try
            {
                var dt = await _db.ExecuteQueryAsync(sql);
                int lowCount = 0, outCount = 0;
                foreach (System.Data.DataRow r in dt.Rows)
                {
                    var qty = r["stock"] != DBNull.Value ? Convert.ToDecimal(r["stock"]) : 0m;
                    var item = new StockAlertItem
                    {
                        MedicineName  = r["medicine"].ToString() ?? "",
                        BatchNumber   = r["batch_number"]?.ToString() ?? "—",
                        StockQty      = qty,
                        ReorderLevel  = r["reorder_level"] != DBNull.Value ? Convert.ToDecimal(r["reorder_level"]) : 0,
                        Supplier      = r["supplier"].ToString() ?? "",
                        Category      = r["category"].ToString() ?? ""
                    };
                    if (qty <= 0) { OutOfStockItems.Add(item); outCount++; }
                    else           { LowStockItems.Add(item);   lowCount++; }
                }
                LowStockCount  = lowCount;
                OutOfStockCount = outCount;
            }
            catch (Exception ex) { System.Console.WriteLine("LowStock Error: " + ex.Message); }
        }

        // ── 2. Expiry Alerts ─────────────────────────────────────────────
        private async Task LoadExpiryAsync()
        {
            ExpiryItems.Clear();

            string sql = $@"
                SELECT
                    p.name          AS medicine,
                    b.batch_number,
                    b.quantity      AS stock,
                    b.expiry_date,
                    (b.expiry_date::date - CURRENT_DATE) AS days_left,
                    COALESCE(s.name,'—') AS supplier
                FROM batches b
                JOIN products p ON p.product_id = b.product_id
                LEFT JOIN suppliers s ON s.supplier_id = (
                    SELECT pi.supplier_id FROM purchases pi
                    JOIN purchase_items pii ON pii.purchase_id = pi.purchase_id
                    WHERE pii.batch_id = b.batch_id
                    ORDER BY pi.purchase_date DESC LIMIT 1
                )
                WHERE b.quantity > 0
                  AND b.expiry_date <= CURRENT_DATE + INTERVAL '{_expiryDays} days'
                ORDER BY b.expiry_date ASC
                LIMIT 100";

            try
            {
                var dt = await _db.ExecuteQueryAsync(sql);
                int expiring = 0, expired = 0;
                foreach (System.Data.DataRow r in dt.Rows)
                {
                    var days = r["days_left"] != DBNull.Value ? Convert.ToInt32(r["days_left"]) : 0;
                    ExpiryItems.Add(new ExpiryAlertItem
                    {
                        MedicineName = r["medicine"].ToString() ?? "",
                        BatchNumber  = r["batch_number"]?.ToString() ?? "",
                        StockQty     = r["stock"] != DBNull.Value ? Convert.ToDecimal(r["stock"]) : 0,
                        ExpiryDate   = r["expiry_date"] != DBNull.Value ? Convert.ToDateTime(r["expiry_date"]) : DateTime.Today,
                        DaysLeft     = days,
                        Supplier     = r["supplier"].ToString() ?? ""
                    });
                    if (days <= 0) expired++; else expiring++;
                }
                ExpiringCount = expiring;
                ExpiredCount  = expired;
            }
            catch (Exception ex) { System.Console.WriteLine("Expiry Error: " + ex.Message); }
        }

        // ── 3. Dead Stock (no movement > IdleDays) ────────────────────────
        private async Task LoadDeadStockAsync()
        {
            DeadStockItems.Clear();

            string sql = $@"
                SELECT
                    p.name         AS medicine,
                    b.batch_number,
                    b.quantity     AS stock,
                    b.created_at   AS last_movement,
                    (CURRENT_DATE - b.created_at::date) AS idle_days
                FROM batches b
                JOIN products p ON p.product_id = b.product_id
                WHERE b.quantity > 0
                  AND b.batch_id NOT IN (
                        SELECT DISTINCT batch_id FROM sale_items
                        WHERE  created_at >= CURRENT_DATE - INTERVAL '{_idleDays} days'
                  )
                  AND b.created_at <= CURRENT_DATE - INTERVAL '{_idleDays} days'
                ORDER BY idle_days DESC
                LIMIT 50";

            try
            {
                var dt = await _db.ExecuteQueryAsync(sql);
                foreach (System.Data.DataRow r in dt.Rows)
                {
                    DeadStockItems.Add(new DeadStockItem
                    {
                        MedicineName  = r["medicine"].ToString() ?? "",
                        BatchNumber   = r["batch_number"]?.ToString() ?? "",
                        StockQty      = r["stock"] != DBNull.Value ? Convert.ToDecimal(r["stock"]) : 0,
                        LastMovement  = r["last_movement"] != DBNull.Value ? Convert.ToDateTime(r["last_movement"]) : DateTime.Today,
                        IdleDays      = r["idle_days"] != DBNull.Value ? Convert.ToInt32(r["idle_days"]) : 0
                    });
                }
                DeadStockCount = DeadStockItems.Count;
            }
            catch (Exception ex) { System.Console.WriteLine("DeadStock Error: " + ex.Message); }
        }
    }
}
