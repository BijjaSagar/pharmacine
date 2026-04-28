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
    public class BatchItem : ViewModelBase
    {
        public int BatchId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public decimal CurrentQuantity { get; set; }
        public DateTime ExpiryDate { get; set; }
    }

    public class StockAdjustmentViewModel : ViewModelBase
    {
        private readonly DatabaseService _dbService;

        private string _barcodeInput = string.Empty;
        public string BarcodeInput
        {
            get => _barcodeInput;
            set => SetProperty(ref _barcodeInput, value);
        }

        public ObservableCollection<BatchItem> Batches { get; set; } = new();

        private BatchItem _selectedBatch;
        public BatchItem SelectedBatch
        {
            get => _selectedBatch;
            set
            {
                SetProperty(ref _selectedBatch, value);
                if (value != null) NewQuantity = value.CurrentQuantity;
            }
        }

        private decimal _newQuantity;
        public decimal NewQuantity
        {
            get => _newQuantity;
            set => SetProperty(ref _newQuantity, value);
        }

        private string _reason = "Breakage";
        public string Reason
        {
            get => _reason;
            set => SetProperty(ref _reason, value);
        }
        
        public ObservableCollection<string> Reasons { get; } = new() { "Breakage", "Expiry", "Audit Correction" };

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

        public ICommand SearchCommand { get; }
        public ICommand AdjustStockCommand { get; }

        public StockAdjustmentViewModel()
        {
            _dbService = new DatabaseService();
            SearchCommand = new RelayCommand(async _ => await SearchBatchesAsync());
            AdjustStockCommand = new RelayCommand(async _ => await AdjustStockAsync());
        }

        private async Task SearchBatchesAsync()
        {
            if (string.IsNullOrWhiteSpace(BarcodeInput)) return;
            ErrorMessage = string.Empty;
            Batches.Clear();

            try
            {
                var sql = @"SELECT b.batch_id, p.name, b.batch_number, b.quantity, b.expiry_date
                            FROM batches b
                            JOIN products p ON b.product_id = p.product_id
                            WHERE p.barcode = @bc";
                            
                var dt = await _dbService.ExecuteQueryAsync(sql, new NpgsqlParameter("@bc", BarcodeInput));

                foreach (System.Data.DataRow row in dt.Rows)
                {
                    Batches.Add(new BatchItem
                    {
                        BatchId = Convert.ToInt32(row["batch_id"]),
                        ProductName = row["name"].ToString() ?? "",
                        BatchNumber = row["batch_number"].ToString() ?? "",
                        CurrentQuantity = Convert.ToDecimal(row["quantity"]),
                        ExpiryDate = Convert.ToDateTime(row["expiry_date"])
                    });
                }

                if (!Batches.Any())
                {
                    ErrorMessage = "No batches found for this barcode.";
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = "DB Error: " + ex.Message;
            }
        }

        private async Task AdjustStockAsync()
        {
            if (SelectedBatch == null)
            {
                ErrorMessage = "Please select a batch first.";
                return;
            }
            if (NewQuantity < 0)
            {
                ErrorMessage = "Quantity cannot be negative.";
                return;
            }
            
            IsBusy = true;
            try
            {
                string logSql = @"INSERT INTO stock_adjustments (batch_id, old_quantity, new_quantity, reason)
                                  VALUES (@bid, @oldq, @newq, @reason)";
                await _dbService.ExecuteNonQueryAsync(logSql,
                    new NpgsqlParameter("@bid", SelectedBatch.BatchId),
                    new NpgsqlParameter("@oldq", SelectedBatch.CurrentQuantity),
                    new NpgsqlParameter("@newq", NewQuantity),
                    new NpgsqlParameter("@reason", Reason));

                string updateSql = "UPDATE batches SET quantity = @qty WHERE batch_id = @bid";
                await _dbService.ExecuteNonQueryAsync(updateSql,
                    new NpgsqlParameter("@qty", NewQuantity),
                    new NpgsqlParameter("@bid", SelectedBatch.BatchId));

                System.Windows.MessageBox.Show("Stock adjusted successfully!", "Success");
                SelectedBatch.CurrentQuantity = NewQuantity;
                ErrorMessage = string.Empty;
                Batches.Clear();
                BarcodeInput = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = "Adjustment failed: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
