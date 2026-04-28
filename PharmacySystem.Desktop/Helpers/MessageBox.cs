using System;

namespace System.Windows
{
    // Mock MessageBox to allow ViewModels to compile unmodified in Avalonia
    public static class MessageBox
    {
        public static void Show(string message, string title = "Notification")
        {
            Console.WriteLine($"[MESSAGE BOX] {title}: {message}");
        }
    }
}
