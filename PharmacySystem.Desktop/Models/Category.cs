namespace PharmacySystem.Desktop.Models
{
    public class Category
    {
        public int CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? ParentId { get; set; }
    }
}
