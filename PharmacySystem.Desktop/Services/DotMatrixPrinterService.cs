using System;
using System.Text;
using System.IO;
using System.Runtime.InteropServices;
using PharmacySystem.Desktop.ViewModels;
using System.Linq;

namespace PharmacySystem.Desktop.Services
{
    public class DotMatrixPrinterService
    {
        // For Dot Matrix, we mostly send plain ASCII text.
        // We use an 80-column width for A5 Landscape / Full A4 half.
        
        public static void PrintInvoice(ConsoleState console, string invoiceNo, string printerName = "LPT1")
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                
                // Header (Pharmacy Info)
                sb.AppendLine("".PadLeft(80, '='));
                sb.AppendLine("CLINICOS PHARMACY".PadLeft(48).PadRight(80));
                sb.AppendLine("123 Health Street, City, State - 400001".PadLeft(59).PadRight(80));
                sb.AppendLine("Phone: +91 98765 43210 | Email: contact@clinicos.com".PadLeft(65).PadRight(80));
                sb.AppendLine($"GSTIN: 22AAAAA0000A1Z5 | DL No: 20B/123/2023, 21B/124/2023".PadLeft(75).PadRight(80));
                sb.AppendLine("TAX INVOICE".PadLeft(45).PadRight(80));
                sb.AppendLine("".PadLeft(80, '-'));

                // Transaction & Customer Meta
                sb.AppendLine($"Invoice No : {invoiceNo,-20} | Date : {DateTime.Now:dd-MMM-yyyy HH:mm}");
                sb.AppendLine($"Patient    : {console.CustomerName,-20} | Doctor : Dr. {console.DoctorName}");
                sb.AppendLine($"Phone      : {console.CustomerPhone,-20} | Address: {console.PatientAddress}");
                sb.AppendLine("".PadLeft(80, '-'));

                // Table Header
                // Columns: SNo(3), Product(30), Batch(10), Exp(7), Qty(6), Rate(8), Disc(5), Amount(9)
                sb.AppendLine("SNo Product Name                   Batch      Exp     Qty    Rate   Disc%   Amount");
                sb.AppendLine("".PadLeft(80, '-'));

                int sno = 1;
                foreach (var item in console.CartItems)
                {
                    string name = item.Product.Name;
                    if (name.Length > 28) name = name.Substring(0, 28);
                    
                    string batch = item.BatchNumber ?? "—";
                    if (batch.Length > 10) batch = batch.Substring(0, 10);

                    string exp = item.ExpiryDate?.ToString("MM/yy") ?? "—";
                    
                    sb.AppendLine($"{sno,3} {name,-28} {batch,-10} {exp,-7} {item.Quantity,6:N0} {item.Product.UnitPrice,7:F2} {item.DiscountPercent,5:F0} {item.Total,8:F2}");
                    sno++;
                }

                sb.AppendLine("".PadLeft(80, '-'));

                // Footer / Totals
                decimal subtotal = console.SubTotal;
                decimal totalGst = console.TotalGst;
                decimal totalDisc = console.DiscountAmount;
                decimal grandTotal = console.GrandTotal;

                sb.AppendLine($"{"Subtotal:",68} {subtotal,10:F2}");
                sb.AppendLine($"{"Total GST:",68} {totalGst,10:F2}");
                sb.AppendLine($"{"Discount:",68} {totalDisc,10:F2}");
                sb.AppendLine("".PadLeft(80, '='));
                sb.AppendLine($"{"GRAND TOTAL:",68} {grandTotal,10:F2}");
                sb.AppendLine("".PadLeft(80, '='));

                // Amount in Words (Simple mock)
                sb.AppendLine($"Amount in words: {AmountToWords(grandTotal)}");
                sb.AppendLine("");
                sb.AppendLine("Terms: Goods once sold will not be taken back. Subject to local jurisdiction.");
                sb.AppendLine("");
                sb.AppendLine("");
                sb.AppendLine("".PadLeft(40) + "For CLINICOS PHARMACY");
                sb.AppendLine("");
                sb.AppendLine("".PadLeft(40) + "(Authorized Signatory)");
                
                // Form Feed (to eject page on Dot Matrix)
                sb.Append("\f");

                string invoiceText = sb.ToString();
                byte[] bytes = Encoding.ASCII.GetBytes(invoiceText);

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    RawPrinterHelper.SendBytesToPrinter(printerName, bytes);
                }

                File.WriteAllText("last_invoice_dotmatrix.txt", invoiceText);
                System.Console.WriteLine($"[DotMatrix] Invoice {invoiceNo} printed to {printerName}.");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Dot Matrix Print Error: " + ex.Message);
            }
        }

        private static string AmountToWords(decimal amount)
        {
            return "Rupees " + amount.ToString("N2") + " Only";
        }
    }
}
