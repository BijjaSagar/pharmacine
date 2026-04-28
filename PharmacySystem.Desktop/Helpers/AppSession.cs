namespace PharmacySystem.Desktop.Helpers
{
    public static class AppSession
    {
        public static int UserId { get; set; }
        public static string Username { get; set; } = string.Empty;
        public static string Role { get; set; } = string.Empty; // "Owner", "Manager", "Cashier"

        public static bool IsOwner => Role == "Owner";
        public static bool IsManager => Role == "Manager";
        public static bool IsCashier => Role == "Cashier";
        public static bool IsOwnerOrManager => IsOwner || IsManager;

        public static void Clear()
        {
            UserId = 0;
            Username = string.Empty;
            Role = string.Empty;
        }
    }
}
