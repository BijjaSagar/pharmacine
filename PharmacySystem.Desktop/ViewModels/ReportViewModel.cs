using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using PharmacySystem.Desktop.Helpers;
using PharmacySystem.Desktop.Models;
using PharmacySystem.Desktop.Services;

namespace PharmacySystem.Desktop.ViewModels
{
    public class ReportViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;

        public ObservableCollection<SalesReportRow> SalesReport { get; set; } = new();
        public ObservableCollection<StockReportRow> StockReport { get; set; } = new();
        public ObservableCollection<GstReportRow> GstReport { get; set; } = new();

        private DateTimeOffset _startDate = DateTimeOffset.Now.AddDays(-30);
        public DateTimeOffset StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        private DateTimeOffset _endDate = DateTimeOffset.Now;
        public DateTimeOffset EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public ICommand LoadReportsCommand { get; }
        public ICommand ExportCommand { get; }

        public ReportViewModel()
        {
            _dbService = new DatabaseService();
            LoadReportsCommand = new RelayCommand(async _ => await LoadAllReportsAsync());
            ExportCommand = new RelayCommand(async type => await ExportToCsvAsync(type?.ToString()));
            
            _ = LoadAllReportsAsync();
        }

        private async Task LoadAllReportsAsync()
        {
            IsBusy = true;
            StatusMessage = "Loading reports...";
            
            try
            {
                await LoadSalesReportAsync();
                await LoadStockReportAsync();
                await LoadGstReportAsync();
                StatusMessage = "Reports loaded successfully.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task LoadSalesReportAsync()
        {
            SalesReport.Clear();
            var sql = @"SELECT sale_date, invoice_number, customer_name, total_amount, payment_mode
                        FROM sales
                        WHERE sale_date >= @start AND sale_date <= @end
                        ORDER BY sale_date DESC";
            
            var dt = await _dbService.ExecuteQueryAsync(sql, 
                new Npgsql.NpgsqlParameter("@start", StartDate.DateTime.Date),
                new Npgsql.NpgsqlParameter("@end", EndDate.DateTime.Date.AddDays(1).AddTicks(-1)));

            foreach (System.Data.DataRow row in dt.Rows)
            {
                SalesReport.Add(new SalesReportRow
                {
                    SaleDate = Convert.ToDateTime(row["sale_date"]),
                    InvoiceNo = row["invoice_number"].ToString() ?? "",
                    CustomerName = row["customer_name"].ToString() ?? "Walk-in",
                    TotalAmount = Convert.ToDecimal(row["total_amount"]),
                    PaymentMode = row["payment_mode"].ToString() ?? ""
                });
            }
        }

        private async Task LoadStockReportAsync()
        {
            StockReport.Clear();
            var sql = @"SELECT p.name, b.batch_number, b.quantity, b.expiry_date, 
                        CURRENT_DATE - b.created_at::date AS age_days
                        FROM batches b
                        JOIN products p ON b.product_id = p.product_id
                        WHERE b.quantity > 0
                        ORDER BY age_days DESC";
            
            var dt = await _dbService.ExecuteQueryAsync(sql);

            foreach (System.Data.DataRow row in dt.Rows)
            {
                StockReport.Add(new StockReportRow
                {
                    ProductName = row["name"].ToString() ?? "",
                    BatchNumber = row["batch_number"].ToString() ?? "",
                    Quantity = Convert.ToInt32(row["quantity"]),
                    ExpiryDate = Convert.ToDateTime(row["expiry_date"]),
                    AgeInDays = Convert.ToInt32(row["age_days"])
                });
            }
        }

        private async Task LoadGstReportAsync()
        {
            GstReport.Clear();
            var sql = @"SELECT invoice_number, sale_date, 
                        total_amount - total_gst AS taxable_amount, 
                        total_gst AS gst_amount, 
                        total_amount,
                        CASE WHEN customer_phone IS NOT NULL AND customer_phone <> '' THEN 'B2C' ELSE 'B2C' END AS type
                        FROM sales
                        WHERE sale_date >= @start AND sale_date <= @end
                        ORDER BY sale_date DESC";
            
            var dt = await _dbService.ExecuteQueryAsync(sql, 
                new Npgsql.NpgsqlParameter("@start", StartDate.DateTime.Date),
                new Npgsql.NpgsqlParameter("@end", EndDate.DateTime.Date.AddDays(1).AddTicks(-1)));

            foreach (System.Data.DataRow row in dt.Rows)
            {
                GstReport.Add(new GstReportRow
                {
                    InvoiceNo = row["invoice_number"].ToString() ?? "",
                    SaleDate = Convert.ToDateTime(row["sale_date"]),
                    TaxableAmount = Convert.ToDecimal(row["taxable_amount"]),
                    GstAmount = Convert.ToDecimal(row["gst_amount"]),
                    TotalAmount = Convert.ToDecimal(row["total_amount"]),
                    Type = row["type"].ToString() ?? ""
                });
            }
        }

        private async Task ExportToCsvAsync(string? type)
        {
            if (string.IsNullOrEmpty(type)) return;

            string fileName = $"Export_{type}_{DateTime.Now:yyyyMMddHHmmss}.csv";
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            
            StringBuilder sb = new StringBuilder();

            try
            {
                if (type == "Sales")
                {
                    sb.AppendLine("Date,Invoice No,Customer Name,Total Amount,Payment Mode");
                    foreach (var item in SalesReport)
                        sb.AppendLine($"{item.SaleDate:g},{item.InvoiceNo},{item.CustomerName},{item.TotalAmount},{item.PaymentMode}");
                }
                else if (type == "Stock")
                {
                    sb.AppendLine("Product Name,Batch Number,Quantity,Expiry Date,Age (Days)");
                    foreach (var item in StockReport)
                        sb.AppendLine($"{item.ProductName},{item.BatchNumber},{item.Quantity},{item.ExpiryDate:d},{item.AgeInDays}");
                }
                else if (type == "GST")
                {
                    sb.AppendLine("Invoice No,Sale Date,Taxable Amount,GST Amount,Total Amount,Type");
                    foreach (var item in GstReport)
                        sb.AppendLine($"{item.InvoiceNo},{item.SaleDate:d},{item.TaxableAmount},{item.GstAmount},{item.TotalAmount},{item.Type}");
                }

                await File.WriteAllTextAsync(path, sb.ToString());
                StatusMessage = $"Export successful! Saved as {fileName}";
            }
            catch (Exception ex)
            {
                StatusMessage = "Export failed: " + ex.Message;
            }
        }
    }
}
