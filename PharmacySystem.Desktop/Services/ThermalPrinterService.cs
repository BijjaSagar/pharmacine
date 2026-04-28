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

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Send to Raw Printer spooler
                    RawPrinterHelper.SendBytesToPrinter(printerName, bytes);
                }
                
                File.WriteAllBytes("last_receipt.bin", bytes);
                System.Console.WriteLine($"[ThermalPrinter] Receipt {invoiceNo} sent to {printerName}. ({bytes.Length} bytes)");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Print Error: " + ex.Message);
            }
        }
    }

    public static class RawPrinterHelper
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public class DOCINFOA
        {
            [MarshalAs(UnmanagedType.LPStr)] public string pDocName;
            [MarshalAs(UnmanagedType.LPStr)] public string pOutputFile;
            [MarshalAs(UnmanagedType.LPStr)] public string pDataType;
        }

        [DllImport("winspool.Drv", EntryPoint = "OpenPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool OpenPrinter([MarshalAs(UnmanagedType.LPStr)] string szPrinter, out IntPtr hPrinter, IntPtr pd);

        [DllImport("winspool.Drv", EntryPoint = "ClosePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool ClosePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartDocPrinterA", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartDocPrinter(IntPtr hPrinter, int level, [In, MarshalAs(UnmanagedType.LPStruct)] DOCINFOA di);

        [DllImport("winspool.Drv", EntryPoint = "EndDocPrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndDocPrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "StartPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool StartPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "EndPagePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool EndPagePrinter(IntPtr hPrinter);

        [DllImport("winspool.Drv", EntryPoint = "WritePrinter", SetLastError = true, ExactSpelling = true, CallingConvention = CallingConvention.StdCall)]
        public static extern bool WritePrinter(IntPtr hPrinter, IntPtr pBytes, int dwCount, out int dwWritten);

        public static bool SendBytesToPrinter(string szPrinterName, byte[] data)
        {
            if (data == null || data.Length == 0) return false;

            IntPtr pUnmanagedBytes = Marshal.AllocCoTaskMem(data.Length);
            Marshal.Copy(data, 0, pUnmanagedBytes, data.Length);
            bool success = SendBytesToPrinter(szPrinterName, pUnmanagedBytes, data.Length);
            Marshal.FreeCoTaskMem(pUnmanagedBytes);
            return success;
        }

        private static bool SendBytesToPrinter(string szPrinterName, IntPtr pBytes, int dwCount)
        {
            int dwError = 0, dwWritten = 0;
            IntPtr hPrinter = new IntPtr(0);
            DOCINFOA di = new DOCINFOA();
            bool bSuccess = false; 

            di.pDocName = "ClinicOS Receipt";
            di.pDataType = "RAW";

            if (OpenPrinter(szPrinterName.Normalize(), out hPrinter, IntPtr.Zero))
            {
                if (StartDocPrinter(hPrinter, 1, di))
                {
                    if (StartPagePrinter(hPrinter))
                    {
                        bSuccess = WritePrinter(hPrinter, pBytes, dwCount, out dwWritten);
                        EndPagePrinter(hPrinter);
                    }
                    EndDocPrinter(hPrinter);
                }
                ClosePrinter(hPrinter);
            }

            if (bSuccess == false)
            {
                dwError = Marshal.GetLastWin32Error();
            }
            return bSuccess;
        }
    }
}
