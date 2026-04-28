using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace PharmacySystem.Desktop.Helpers
{
    public static class BackupHelper
    {
        public static async Task<string> BackupDatabaseAsync()
        {
            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                if (!File.Exists(configPath))
                    throw new FileNotFoundException("appsettings.json not found.");

                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                var dbNode = doc.RootElement.GetProperty("Database");

                var server = dbNode.GetProperty("Server").GetString();
                var port = dbNode.GetProperty("Port").GetInt32();
                var database = dbNode.GetProperty("DatabaseName").GetString();
                var user = dbNode.GetProperty("UserId").GetString();
                var pass = dbNode.GetProperty("Password").GetString();

                // Define backup path
                string backupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups");
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }

                string fileName = $"Backup_{database}_{DateTime.Now:yyyyMMdd_HHmmss}.sql";
                string fullPath = Path.Combine(backupDir, fileName);

                string pgDumpPath = "pg_dump"; // Assumes pg_dump is in PATH

                // On macOS/Linux, it's just pg_dump. On Windows, it might need the full path to PostgreSQL bin folder.
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    // This is a common path, adjust if necessary
                    var possiblePath = Path.Combine(programFiles, "PostgreSQL", "14", "bin", "pg_dump.exe");
                    if (File.Exists(possiblePath))
                    {
                        pgDumpPath = possiblePath;
                    }
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = pgDumpPath,
                    Arguments = $"-h {server} -p {port} -U {user} -F c -b -v -f \"{fullPath}\" {database}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                startInfo.EnvironmentVariables["PGPASSWORD"] = pass;

                using var process = new Process { StartInfo = startInfo };
                process.Start();

                await process.WaitForExitAsync();

                if (process.ExitCode != 0)
                {
                    string error = await process.StandardError.ReadToEndAsync();
                    throw new Exception($"pg_dump failed with exit code {process.ExitCode}: {error}");
                }

                return fullPath;
            }
            catch (Exception ex)
            {
                throw new Exception("Backup failed: " + ex.Message, ex);
            }
        }
    }
}
