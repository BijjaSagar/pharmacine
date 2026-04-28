namespace PharmacySystem.Desktop.Models
{
    public class User
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string OverridePin { get; set; } = "1234";
        public bool IsActive { get; set; }
    }
}
