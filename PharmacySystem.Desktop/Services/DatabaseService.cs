using Npgsql;
using System;
using System.Data;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace PharmacySystem.Desktop.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService()
        {
            _connectionString = BuildConnectionString();
        }

        private string BuildConnectionString()
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
                var pass = dbNode.GetProperty("Password").GetString(); // Decrypt if necessary in real scenario

                return $"Host={server};Port={port};Database={database};Username={user};Password={pass};Pooling=true;Timeout=30;";
            }
            catch (Exception ex)
            {
                // Fallback or log error
                throw new Exception("Failed to load database configuration.", ex);
            }
        }

        public async Task<NpgsqlConnection> GetOpenConnectionAsync()
        {
            var connection = new NpgsqlConnection(_connectionString);
            int maxRetries = 3;
            int delayMs = 1000;

            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    await connection.OpenAsync();
                    return connection;
                }
                catch (Exception ex)
                {
                    if (i == maxRetries - 1)
                    {
                        Console.WriteLine($"DB Connection Failed after {maxRetries} attempts: " + ex.Message);
                        throw;
                    }
                    await Task.Delay(delayMs);
                    delayMs *= 2; // Exponential backoff
                }
            }
            return connection;
        }

        public async Task<DataTable> ExecuteQueryAsync(string sql, params NpgsqlParameter[] parameters)
        {
            using var connection = await GetOpenConnectionAsync();
            using var command = new NpgsqlCommand(sql, connection);
            
            if (parameters != null)
                command.Parameters.AddRange(parameters);

            using var reader = await command.ExecuteReaderAsync();
            var dataTable = new DataTable();
            dataTable.Load(reader);
            
            return dataTable;
        }

        public async Task<int> ExecuteNonQueryAsync(string sql, params NpgsqlParameter[] parameters)
        {
            using var connection = await GetOpenConnectionAsync();
            using var command = new NpgsqlCommand(sql, connection);

            if (parameters != null)
                command.Parameters.AddRange(parameters);

            return await command.ExecuteNonQueryAsync();
        }

        public async Task<object> ExecuteScalarAsync(string sql, params NpgsqlParameter[] parameters)
        {
            using var connection = await GetOpenConnectionAsync();
            using var command = new NpgsqlCommand(sql, connection);

            if (parameters != null)
                command.Parameters.AddRange(parameters);

            return await command.ExecuteScalarAsync();
        }
    }
}
