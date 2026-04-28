namespace PharmacySystem.Desktop.Models
{
    public class Product
    {
        public int ProductId { get; set; }
        public string Barcode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string GenericName { get; set; } = string.Empty;
        public int? CategoryId { get; set; }
        public string PackSize { get; set; } = string.Empty;
        public int ReorderLevel { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal GstPercent { get; set; }
        public bool IsPrescriptionRequired { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
