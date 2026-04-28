namespace PharmacySystem.Desktop.Helpers
{
    public static class AppSession
    {
        public static int UserId { get; set; }
        public static string Username { get; set; } = string.Empty;
        public static string Role { get; set; } = string.Empty; // "Admin", "Manager", "Biller"

        public static bool IsAdmin => Role == "Admin";
        public static bool IsManager => Role == "Manager";
        public static bool IsBiller => Role == "Biller";

        public static void Clear()
        {
            UserId = 0;
            Username = string.Empty;
            Role = string.Empty;
        }
    }
}
