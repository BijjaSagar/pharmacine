using System;

namespace PharmacySystem.Desktop.Models
{
    public class SalesReportRow
    {
        public DateTime SaleDate { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public string PaymentMode { get; set; } = string.Empty;
    }

    public class StockReportRow
    {
        public string ProductName { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public decimal Quantity { get; set; }
        public DateTime ExpiryDate { get; set; }
        public int AgeInDays { get; set; } // Based on created_at vs now
    }

    public class GstReportRow
    {
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public string HSNCode { get; set; } = string.Empty;
        public decimal TaxableAmount { get; set; }
        public decimal GstAmount { get; set; }
        public decimal CGST => GstAmount / 2;
        public decimal SGST => GstAmount / 2;
        public decimal TotalAmount { get; set; }
        public string Type { get; set; } = string.Empty; // B2B or B2C
    }
}
