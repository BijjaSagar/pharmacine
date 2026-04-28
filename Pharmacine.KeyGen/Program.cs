using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Pharmacine.KeyGen
{
    class Program
    {
        private static readonly string Secret = "PharmacyKeyMaker2026!";

        static void Main(string[] args)
        {
            Console.WriteLine("=====================================");
            Console.WriteLine(" PHARMACINE LICENSE KEY GENERATOR");
            Console.WriteLine("=====================================\n");

            Console.Write("Enter Client Hardware ID: ");
            string hwid = Console.ReadLine()?.Trim() ?? "";

            Console.Write("Enter Duration in Days (e.g., 365 for 1 year): ");
            if (!int.TryParse(Console.ReadLine(), out int days))
            {
                days = 365;
            }

            string payload = $"{hwid}|{DateTime.Now.AddDays(days):O}";
            string licenseKey = Encrypt(payload);

            Console.WriteLine("\n[SUCCESS] Key Generated:");
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine(licenseKey);
            Console.WriteLine("--------------------------------------------------");
            Console.WriteLine("Copy this key and send it to the client.");
            Console.ReadLine();
        }

        private static string Encrypt(string clearText)
        {
            byte[] clearBytes = Encoding.Unicode.GetBytes(clearText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(Secret, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(clearBytes, 0, clearBytes.Length);
                        cs.Close();
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }
    }
}
