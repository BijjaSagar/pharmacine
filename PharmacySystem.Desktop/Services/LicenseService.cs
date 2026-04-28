using System;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;

namespace PharmacySystem.Desktop.Services
{
    public class LicenseService
    {
        private static readonly string Secret = "PharmacyKeyMaker2026!";
        private static readonly string AppDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pharmacine");
        private static readonly string LicenseFile = Path.Combine(AppDataFolder, "license.dat");
        private static readonly string TrialFile = Path.Combine(AppDataFolder, "sys_info.dat");

        public static string GetHardwareId()
        {
            try
            {
                var macAddr = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == OperationalStatus.Up && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Select(nic => nic.GetPhysicalAddress().ToString())
                    .FirstOrDefault();

                if (string.IsNullOrEmpty(macAddr))
                    macAddr = Environment.MachineName;

                // Simple hash to make it look clean
                using (var sha = SHA256.Create())
                {
                    var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(macAddr + "PHARMA"));
                    return BitConverter.ToString(hash).Replace("-", "").Substring(0, 16); // 16 char ID
                }
            }
            catch
            {
                return "UNKNOWN-HWID";
            }
        }

        public static LicenseStatus CheckLicense()
        {
            if (!Directory.Exists(AppDataFolder))
                Directory.CreateDirectory(AppDataFolder);

            // 1. Check Full License
            if (File.Exists(LicenseFile))
            {
                var key = File.ReadAllText(LicenseFile).Trim();
                if (ValidateKey(key, out DateTime expiry))
                {
                    if (DateTime.Now <= expiry)
                    {
                        return new LicenseStatus { IsValid = true, Message = $"Licensed. Expires: {expiry:d}", IsTrial = false, ExpiryDate = expiry };
                    }
                    else
                    {
                        return new LicenseStatus { IsValid = false, Message = "License Expired!", IsTrial = false };
                    }
                }
            }

            // 2. Check 7-Day Trial
            if (!File.Exists(TrialFile))
            {
                // Start trial today
                File.WriteAllText(TrialFile, Encrypt(DateTime.Now.ToString("O")));
            }

            try
            {
                var firstUseStr = Decrypt(File.ReadAllText(TrialFile));
                if (DateTime.TryParse(firstUseStr, out DateTime firstUse))
                {
                    var daysLeft = 7 - (DateTime.Now - firstUse).TotalDays;
                    if (daysLeft > 0)
                    {
                        return new LicenseStatus { IsValid = true, Message = $"Trial Mode. {Math.Ceiling(daysLeft)} days left.", IsTrial = true, ExpiryDate = firstUse.AddDays(7) };
                    }
                }
            }
            catch { }

            return new LicenseStatus { IsValid = false, Message = "Trial Expired. Please purchase a license key.", IsTrial = true };
        }

        public static bool ActivateLicense(string key)
        {
            if (ValidateKey(key, out _))
            {
                if (!Directory.Exists(AppDataFolder))
                    Directory.CreateDirectory(AppDataFolder);
                
                File.WriteAllText(LicenseFile, key);
                return true;
            }
            return false;
        }

        // Developer Tool Logic: GenKey
        public static string GenerateKey(string hwid, int daysValid)
        {
            string payload = $"{hwid}|{DateTime.Now.AddDays(daysValid):O}";
            return Encrypt(payload);
        }

        private static bool ValidateKey(string key, out DateTime expiry)
        {
            expiry = DateTime.MinValue;
            try
            {
                string payload = Decrypt(key);
                var parts = payload.Split('|');
                if (parts.Length == 2)
                {
                    string hwid = parts[0];
                    if (hwid == GetHardwareId())
                    {
                        return DateTime.TryParse(parts[1], out expiry);
                    }
                }
            }
            catch { }
            return false;
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

        private static string Decrypt(string cipherText)
        {
            cipherText = cipherText.Replace(" ", "+");
            byte[] cipherBytes = Convert.FromBase64String(cipherText);
            using (Aes encryptor = Aes.Create())
            {
                Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(Secret, new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 });
                encryptor.Key = pdb.GetBytes(32);
                encryptor.IV = pdb.GetBytes(16);
                using (MemoryStream ms = new MemoryStream())
                {
                    using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(cipherBytes, 0, cipherBytes.Length);
                        cs.Close();
                    }
                    return Encoding.Unicode.GetString(ms.ToArray());
                }
            }
        }
    }

    public class LicenseStatus
    {
        public bool IsValid { get; set; }
        public string Message { get; set; } = string.Empty;
        public bool IsTrial { get; set; }
        public DateTime ExpiryDate { get; set; }
    }
}
