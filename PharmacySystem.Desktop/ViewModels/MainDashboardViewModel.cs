using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using PharmacySystem.Desktop.Helpers;
using PharmacySystem.Desktop.Views;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Threading.Tasks;

namespace PharmacySystem.Desktop.ViewModels
{
    public class MainDashboardViewModel : ViewModelBase
    {
        public string CurrentDate => DateTime.Now.ToString("dd MMM yyyy, dddd");
        public string LoggedInUser => $"{Helpers.AppSession.Username}  [{Helpers.AppSession.Role}]";

        public ICommand OpenPosCommand { get; }
        public ICommand OpenProductsCommand { get; }
        public ICommand OpenSuppliersCommand { get; }
        public ICommand OpenPurchaseCommand { get; }
        public ICommand OpenStockAdjustmentCommand { get; }
        public ICommand OpenUsersCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand OpenReportsCommand { get; }
        public ICommand OpenInventoryAlertsCommand { get; }

        public bool IsOwner => Helpers.AppSession.IsOwner;
        public bool IsManager => Helpers.AppSession.IsManager;
        public bool IsOwnerOrManager => IsOwner || IsManager;

        private string _todaySales = "₹ 0.00";
        public string TodaySales { get => _todaySales; set => SetProperty(ref _todaySales, value); }

        private string _totalOrders = "0";
        public string TotalOrders { get => _totalOrders; set => SetProperty(ref _totalOrders, value); }

        private string _lowStockItems = "0";
        public string LowStockItems { get => _lowStockItems; set => SetProperty(ref _lowStockItems, value); }
        
        private string _expiringItems = "0";
        public string ExpiringItems { get => _expiringItems; set => SetProperty(ref _expiringItems, value); }

        private string _outOfStockItems = "0";
        public string OutOfStockItems { get => _outOfStockItems; set => SetProperty(ref _outOfStockItems, value); }

        private string _expiredItems = "0";
        public string ExpiredItems { get => _expiredItems; set => SetProperty(ref _expiredItems, value); }

        private ISeries[] _salesSeries;
        public ISeries[] SalesSeries { get => _salesSeries; set => SetProperty(ref _salesSeries, value); }

        private Axis[] _xAxes;
        public Axis[] XAxes { get => _xAxes; set => SetProperty(ref _xAxes, value); }

        private readonly Services.DatabaseService _dbService;

        public MainDashboardViewModel()
        {
            _dbService = new Services.DatabaseService();
            OpenPosCommand              = new RelayCommand(_ => new POSView().Show());
            OpenProductsCommand         = new RelayCommand(_ => new ProductView().Show());
            OpenSuppliersCommand        = new RelayCommand(_ => new SupplierView().Show());
            OpenPurchaseCommand         = new RelayCommand(_ => new PurchaseView().Show());
            OpenStockAdjustmentCommand  = new RelayCommand(_ => new StockAdjustmentView().Show());
            OpenUsersCommand            = new RelayCommand(_ => new UserView().Show());
            OpenSettingsCommand         = new RelayCommand(_ => new SettingsView().Show());
            OpenReportsCommand          = new RelayCommand(_ => new ReportView().Show());
            OpenInventoryAlertsCommand  = new RelayCommand(_ => new InventoryAlertView().Show());

            _ = LoadDashboardStatsAsync();
        }

        private async System.Threading.Tasks.Task LoadDashboardStatsAsync()
        {
            try
            {
                // Sales
                var salesSql = "SELECT COALESCE(SUM(grand_total), 0) as total, COUNT(*) as cnt FROM sales WHERE DATE(sale_date) = CURRENT_DATE AND is_cancelled = false";
                var salesDt = await _dbService.ExecuteQueryAsync(salesSql);
                if (salesDt.Rows.Count > 0)
                {
                    TodaySales = $"₹ {Convert.ToDecimal(salesDt.Rows[0]["total"]):N2}";
                    TotalOrders = salesDt.Rows[0]["cnt"].ToString() ?? "0";
                }

                // Low Stock
                var lowStockSql = "SELECT COUNT(*) FROM products p JOIN batches b ON p.product_id = b.product_id WHERE b.quantity > 0 AND b.quantity <= p.reorder_level AND p.reorder_level > 0";
                var lsObj = await _dbService.ExecuteScalarAsync(lowStockSql);
                LowStockItems = lsObj?.ToString() ?? "0";

                // Out of Stock
                var oosSql = "SELECT COUNT(DISTINCT p.product_id) FROM products p WHERE p.is_active = true AND NOT EXISTS (SELECT 1 FROM batches b WHERE b.product_id = p.product_id AND b.quantity > 0)";
                var oosObj = await _dbService.ExecuteScalarAsync(oosSql);
                OutOfStockItems = oosObj?.ToString() ?? "0";

                // Expiring < 30 days
                var expSql = "SELECT COUNT(*) FROM batches WHERE expiry_date <= CURRENT_DATE + INTERVAL '30 days' AND expiry_date > CURRENT_DATE AND quantity > 0";
                var expObj = await _dbService.ExecuteScalarAsync(expSql);
                ExpiringItems = expObj?.ToString() ?? "0";

                // Already Expired but still in stock
                var expiredSql = "SELECT COUNT(*) FROM batches WHERE expiry_date < CURRENT_DATE AND quantity > 0";
                var expiredObj = await _dbService.ExecuteScalarAsync(expiredSql);
                ExpiredItems = expiredObj?.ToString() ?? "0";

                // Generate Chart Data for the last 7 days
                var chartSql = @"
                    WITH dates AS (
                        SELECT generate_series(CURRENT_DATE - INTERVAL '6 days', CURRENT_DATE, '1 day')::date AS date
                    )
                    SELECT d.date, COALESCE(SUM(s.grand_total), 0) as total
                    FROM dates d
                    LEFT JOIN sales s ON DATE(s.sale_date) = d.date
                    GROUP BY d.date
                    ORDER BY d.date;
                ";
                var chartDt = await _dbService.ExecuteQueryAsync(chartSql);
                
                var values = new List<double>();
                var labels = new List<string>();

                foreach (System.Data.DataRow row in chartDt.Rows)
                {
                    labels.Add(Convert.ToDateTime(row["date"]).ToString("dd MMM"));
                    values.Add(Convert.ToDouble(row["total"]));
                }

                SalesSeries = new ISeries[]
                {
                    new LineSeries<double>
                    {
                        Values = values,
                        Fill = null,
                        Name = "Daily Sales (₹)",
                        GeometrySize = 10,
                        LineSmoothness = 0.5
                    }
                };

                XAxes = new Axis[]
                {
                    new Axis
                    {
                        Labels = labels,
                        Name = "Date",
                        LabelsRotation = 15
                    }
                };
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Dashboard Error: " + ex.Message);
            }
        }
    }
}
