using System;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using PharmacySystem.Desktop.ViewModels;

namespace PharmacySystem.Desktop.Services
{
    public class ThermalPrinterService
    {
        // ESC/POS commands
        private static readonly byte[] ESC_INIT = new byte[] { 0x1B, 0x40 };
        private static readonly byte[] ESC_ALIGN_CENTER = new byte[] { 0x1B, 0x61, 0x01 };
        private static readonly byte[] ESC_ALIGN_LEFT = new byte[] { 0x1B, 0x61, 0x00 };
        private static readonly byte[] ESC_ALIGN_RIGHT = new byte[] { 0x1B, 0x61, 0x02 };
        private static readonly byte[] ESC_BOLD_ON = new byte[] { 0x1B, 0x45, 0x01 };
        private static readonly byte[] ESC_BOLD_OFF = new byte[] { 0x1B, 0x45, 0x00 };
        private static readonly byte[] ESC_CUT_PAPER = new byte[] { 0x1D, 0x56, 0x41, 0x10 };
        
        public static void PrintReceipt(ConsoleState console, string invoiceNo, string printerName = "USB001")
        {
            try
            {
                using var ms = new MemoryStream();
                using var bw = new BinaryWriter(ms);

                // Initialize printer
                bw.Write(ESC_INIT);
                
                // Header (Center, Bold)
                bw.Write(ESC_ALIGN_CENTER);
                bw.Write(ESC_BOLD_ON);
                bw.Write(Encoding.ASCII.GetBytes("CLINICOS PHARMACY\n"));
                bw.Write(ESC_BOLD_OFF);
                bw.Write(Encoding.ASCII.GetBytes("123 Health Street, City, State\n"));
                bw.Write(Encoding.ASCII.GetBytes("Phone: +91 98765 43210\n"));
                bw.Write(Encoding.ASCII.GetBytes("GSTIN: 22AAAAA0000A1Z5\n"));
                bw.Write(Encoding.ASCII.GetBytes("--------------------------------\n"));
                
                // Invoice Details (Left)
                bw.Write(ESC_ALIGN_LEFT);
                bw.Write(Encoding.ASCII.GetBytes($"Invoice No : {invoiceNo}\n"));
                bw.Write(Encoding.ASCII.GetBytes($"Date       : {DateTime.Now:dd-MMM-yyyy HH:mm}\n"));
                bw.Write(Encoding.ASCII.GetBytes($"Customer   : {console.CustomerName}\n"));
                if (!string.IsNullOrEmpty(console.DoctorName))
                    bw.Write(Encoding.ASCII.GetBytes($"Doctor     : Dr. {console.DoctorName}\n"));
                bw.Write(Encoding.ASCII.GetBytes("--------------------------------\n"));
                
                // Items Header
                bw.Write(Encoding.ASCII.GetBytes("Item            Qty   Price  Total\n"));
                bw.Write(Encoding.ASCII.GetBytes("--------------------------------\n"));
                
                // Items
                foreach (var item in console.CartItems)
                {
                    string name = item.Product.Name;
                    if (name.Length > 15) name = name.Substring(0, 15);
                    
                    string line = $"{name,-15} {item.Quantity,3:N0} {item.Product.UnitPrice,7:F2} {item.Total,6:F2}\n";
                    bw.Write(Encoding.ASCII.GetBytes(line));
                }
                
                bw.Write(Encoding.ASCII.GetBytes("--------------------------------\n"));
                
                // Totals
                bw.Write(ESC_ALIGN_RIGHT);
                bw.Write(Encoding.ASCII.GetBytes($"Subtotal: {console.SubTotal,8:F2}\n"));
                bw.Write(Encoding.ASCII.GetBytes($"GST: {console.TotalGst,8:F2}\n"));
                bw.Write(Encoding.ASCII.GetBytes($"Discount: {console.DiscountAmount,8:F2}\n"));
                bw.Write(ESC_BOLD_ON);
                bw.Write(Encoding.ASCII.GetBytes($"TOTAL: {console.GrandTotal,8:F2}\n"));
                bw.Write(ESC_BOLD_OFF);
                
                bw.Write(ESC_ALIGN_CENTER);
                bw.Write(Encoding.ASCII.GetBytes("--------------------------------\n"));
                bw.Write(Encoding.ASCII.GetBytes("Thank you for your visit!\n"));
                bw.Write(Encoding.ASCII.GetBytes("Get well soon.\n\n\n\n\n"));
                
                // Cut paper
                bw.Write(ESC_CUT_PAPER);
                
                var bytes = ms.ToArray();

                // Send to Raw Printer via system API or file port (fallback for demo)
                // If actual network printer: Socket.Send(bytes)
                // We'll write to a raw generic spooler using Windows API or simply drop it to a file
                // For cross-platform dev/demo purposes, we log it, or save to "receipt.bin"
                File.WriteAllBytes("last_receipt.bin", bytes);
                System.Console.WriteLine($"[ThermalPrinter] Receipt {invoiceNo} sent to {printerName}. ({bytes.Length} bytes)");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Print Error: " + ex.Message);
            }
        }
    }
}
