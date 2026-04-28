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
        private readonly AiService _aiService;

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

        private string _aiChurnMessage = string.Empty;
        public string AiChurnMessage
        {
            get => _aiChurnMessage;
            set => SetProperty(ref _aiChurnMessage, value);
        }

        public ICommand LoadReportsCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand AnalyzeChurnCommand { get; }

        public ReportViewModel()
        {
            _dbService = new DatabaseService();
            _aiService = new AiService();
            LoadReportsCommand = new RelayCommand(async _ => await LoadAllReportsAsync());
            ExportCommand = new RelayCommand(async type => await ExportToCsvAsync(type?.ToString()));
            AnalyzeChurnCommand = new RelayCommand(async _ => await RunAiChurnAnalysisAsync());
            
            _ = LoadAllReportsAsync();
        }

        private async Task RunAiChurnAnalysisAsync()
        {
            if (!_aiService.IsConfigured) { AiChurnMessage = "AI Key not configured in appsettings.json"; return; }
            IsBusy = true;
            AiChurnMessage = "AI is analyzing customer purchase patterns...";
            try
            {
                var sql = @"
                    SELECT customer_name, customer_phone, MAX(sale_date) as last_visit, COUNT(*) as visit_count 
                    FROM sales 
                    WHERE customer_name IS NOT NULL AND customer_name != '' AND customer_phone IS NOT NULL AND customer_phone != ''
                    GROUP BY customer_name, customer_phone
                    HAVING MAX(sale_date) < CURRENT_DATE - INTERVAL '60 days' AND COUNT(*) > 1
                    ORDER BY last_visit DESC LIMIT 30";
                
                var dt = await _dbService.ExecuteQueryAsync(sql);
                var churnData = new System.Collections.Generic.List<string>();
                foreach (System.Data.DataRow r in dt.Rows)
                {
                    churnData.Add($"{r["customer_name"]} (Ph: {r["customer_phone"]}) - Visits: {r["visit_count"]}, Last Visit: {Convert.ToDateTime(r["last_visit"]):yyyy-MM-dd}");
                }

                if (churnData.Count == 0)
                {
                    AiChurnMessage = "No churning customers found in the selected timeframe.";
                    return;
                }

                AiChurnMessage = await _aiService.AnalyzeChurnAsync(string.Join("\n", churnData));
            }
            catch (Exception ex)
            {
                AiChurnMessage = "AI Error: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
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
            var sql = @"SELECT s.invoice_number, s.sale_date, 
                        s.total_amount - s.total_gst AS taxable_amount, 
                        s.total_gst AS gst_amount, 
                        s.total_amount,
                        STRING_AGG(DISTINCT COALESCE(p.hsn_code, '3004'), ', ') as hsn_codes,
                        CASE WHEN s.customer_phone IS NOT NULL AND s.customer_phone <> '' THEN 'B2C' ELSE 'B2C' END AS type
                        FROM sales s
                        LEFT JOIN sale_items si ON s.sale_id = si.sale_id
                        LEFT JOIN batches b ON si.batch_id = b.batch_id
                        LEFT JOIN products p ON b.product_id = p.product_id
                        WHERE s.sale_date >= @start AND s.sale_date <= @end
                        GROUP BY s.sale_id, s.invoice_number, s.sale_date, s.total_amount, s.total_gst, s.customer_phone
                        ORDER BY s.sale_date DESC";
            
            var dt = await _dbService.ExecuteQueryAsync(sql, 
                new Npgsql.NpgsqlParameter("@start", StartDate.DateTime.Date),
                new Npgsql.NpgsqlParameter("@end", EndDate.DateTime.Date.AddDays(1).AddTicks(-1)));

            foreach (System.Data.DataRow row in dt.Rows)
            {
                GstReport.Add(new GstReportRow
                {
                    InvoiceNo = row["invoice_number"].ToString() ?? "",
                    SaleDate = Convert.ToDateTime(row["sale_date"]),
                    HSNCode = row["hsn_codes"].ToString() ?? "3004",
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
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string path = Path.Combine(desktopPath, fileName);
            
            StringBuilder sb = new StringBuilder();

            try
            {
                if (type == "H1")
                {
                    StatusMessage = "Generating Schedule H1 Register...";
                    var sql = @"
                        SELECT s.sale_date, s.invoice_no, s.customer_name, s.patient_address, s.doctor_name, 
                               p.name as medicine, si.quantity
                        FROM sales s
                        JOIN sale_items si ON s.sale_id = si.sale_id
                        JOIN batches b ON si.batch_id = b.batch_id
                        JOIN products p ON b.product_id = p.product_id
                        WHERE p.is_schedule_h1 = true
                          AND s.sale_date >= @start AND s.sale_date <= @end
                        ORDER BY s.sale_date DESC";
                    
                    var dt = await _dbService.ExecuteQueryAsync(sql, 
                        new Npgsql.NpgsqlParameter("@start", StartDate.DateTime.Date),
                        new Npgsql.NpgsqlParameter("@end", EndDate.DateTime.Date.AddDays(1).AddTicks(-1)));
                        
                    sb.AppendLine("Date,Invoice No,Patient Name,Patient Address,Prescribing Doctor,Medicine,Quantity");
                    foreach (System.Data.DataRow row in dt.Rows)
                    {
                        var d = Convert.ToDateTime(row["sale_date"]).ToString("g");
                        var inv = row["invoice_no"].ToString();
                        var pName = row["customer_name"].ToString()?.Replace(",", " ");
                        var pAddr = row["patient_address"].ToString()?.Replace(",", " ");
                        var doc = row["doctor_name"].ToString()?.Replace(",", " ");
                        var med = row["medicine"].ToString()?.Replace(",", " ");
                        var qty = row["quantity"].ToString();
                        
                        sb.AppendLine($"{d},{inv},{pName},{pAddr},{doc},{med},{qty}");
                    }
                }
                else if (type == "Sales")
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
                    sb.AppendLine("Invoice No,Sale Date,HSN Code,Taxable Amount,CGST,SGST,Total Amount,Type");
                    foreach (var item in GstReport)
                        sb.AppendLine($"{item.InvoiceNo},{item.SaleDate:d},\"{item.HSNCode}\",{item.TaxableAmount},{item.CGST:F2},{item.SGST:F2},{item.TotalAmount},{item.Type}");
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
