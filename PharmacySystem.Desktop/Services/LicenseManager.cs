using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text.Json;

namespace PharmacySystem.Desktop.Services
{
    public class LicenseManager
    {
        private static readonly string Secret = "PharmacyKeyMaker2026!";
        private static readonly string LicenseFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClinicOS", "license.dat");

        public class LicenseState
        {
            public DateTime InstallDate { get; set; }
            public string LicenseKey { get; set; } = string.Empty;
        }

        public static string GetHardwareId()
        {
            var macAddr = (
                from nic in NetworkInterface.GetAllNetworkInterfaces()
                where nic.OperationalStatus == OperationalStatus.Up
                select nic.GetPhysicalAddress().ToString()
            ).FirstOrDefault();

            return macAddr ?? "DEFAULT-HWID";
        }

        public static bool IsLicenseValid(out string message)
        {
            try
            {
                var dir = Path.GetDirectoryName(LicenseFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                LicenseState state;
                if (!File.Exists(LicenseFilePath))
                {
                    state = new LicenseState { InstallDate = DateTime.Now };
                    File.WriteAllText(LicenseFilePath, JsonSerializer.Serialize(state));
                }
                else
                {
                    state = JsonSerializer.Deserialize<LicenseState>(File.ReadAllText(LicenseFilePath)) ?? new LicenseState { InstallDate = DateTime.Now };
                }

                // Check active valid key
                if (!string.IsNullOrEmpty(state.LicenseKey))
                {
                    if (ValidateKey(state.LicenseKey, out DateTime expiry))
                    {
                        if (DateTime.Now <= expiry)
                        {
                            message = $"Licensed until {expiry:dd-MMM-yyyy}";
                            return true;
                        }
                        else
                        {
                            message = "License Expired. Please contact support.";
                            return false;
                        }
                    }
                }

                // Fallback to Trial
                var trialDaysLeft = 7 - (DateTime.Now - state.InstallDate).TotalDays;
                if (trialDaysLeft > 0)
                {
                    message = $"Trial Mode: {Math.Ceiling(trialDaysLeft)} days remaining.";
                    return true;
                }

                message = "Trial Expired. Please purchase a license key.";
                return false;
            }
            catch (Exception ex)
            {
                message = "License Error: " + ex.Message;
                return false;
            }
        }

        public static bool ActivateLicense(string key)
        {
            if (ValidateKey(key, out _))
            {
                var dir = Path.GetDirectoryName(LicenseFilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                LicenseState state = new LicenseState { InstallDate = DateTime.Now };
                if (File.Exists(LicenseFilePath))
                {
                    state = JsonSerializer.Deserialize<LicenseState>(File.ReadAllText(LicenseFilePath)) ?? state;
                }

                state.LicenseKey = key;
                File.WriteAllText(LicenseFilePath, JsonSerializer.Serialize(state));
                return true;
            }
            return false;
        }

        private static bool ValidateKey(string key, out DateTime expiry)
        {
            expiry = DateTime.MinValue;
            try
            {
                string decrypted = Decrypt(key);
                string[] parts = decrypted.Split('|');
                if (parts.Length == 2)
                {
                    string hwid = parts[0];
                    if (hwid == GetHardwareId() && DateTime.TryParse(parts[1], out expiry))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private static string Decrypt(string cipherText)
        {
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
}
