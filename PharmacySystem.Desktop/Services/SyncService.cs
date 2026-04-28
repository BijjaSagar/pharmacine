using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PharmacySystem.Desktop.Helpers;

namespace PharmacySystem.Desktop.Services
{
    public class SyncService
    {
        private readonly DatabaseService _dbService;
        private readonly HttpClient _httpClient;
        private CancellationTokenSource _cancellationTokenSource;

        public SyncService()
        {
            _dbService = new DatabaseService();
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri("https://api.clinic0s.com/pharma/"); // Placeholder Cloud API
        }

        public void StartSyncWorker()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _ = Task.Run(() => SyncLoopAsync(_cancellationTokenSource.Token));
        }

        public void StopSyncWorker()
        {
            _cancellationTokenSource?.Cancel();
        }

        private async Task SyncLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await PushPendingChangesAsync();
                    await PullRemoteChangesAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Sync Error: {ex.Message}");
                }

                // Wait 5 minutes before next sync
                await Task.Delay(TimeSpan.FromMinutes(5), token);
            }
        }

        private async Task PushPendingChangesAsync()
        {
            // Fetch from sync_queue
            var sql = "SELECT sync_id, table_name, record_id, operation FROM sync_queue WHERE sync_status = 'PENDING'";
            var dt = await _dbService.ExecuteQueryAsync(sql);

            foreach (System.Data.DataRow row in dt.Rows)
            {
                int syncId = Convert.ToInt32(row["sync_id"]);
                string table = row["table_name"].ToString();
                int recordId = Convert.ToInt32(row["record_id"]);
                string operation = row["operation"].ToString();

                // Fake API POST
                var payload = $"{{\"table\": \"{table}\", \"id\": {recordId}, \"op\": \"{operation}\"}}";
                var content = new StringContent(payload, Encoding.UTF8, "application/json");
                
                try
                {
                    // Simulated POST
                    // var response = await _httpClient.PostAsync("sync", content);
                    // if (response.IsSuccessStatusCode)
                    
                    // Mark as completed
                    await _dbService.ExecuteNonQueryAsync("UPDATE sync_queue SET sync_status = 'COMPLETED' WHERE sync_id = @id",
                        new Npgsql.NpgsqlParameter("@id", syncId));
                }
                catch (Exception)
                {
                    // Mark as failed
                    await _dbService.ExecuteNonQueryAsync("UPDATE sync_queue SET sync_status = 'FAILED' WHERE sync_id = @id",
                        new Npgsql.NpgsqlParameter("@id", syncId));
                }
            }
        }

        private async Task PullRemoteChangesAsync()
        {
            // Simulate Pulling changes from server
            // Compare last_modified timestamps
            // If remote > local, Update local DB
            await Task.CompletedTask;
        }
    }
}
