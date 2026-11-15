using System.Text;
using MySqlConnector;

namespace BankarskiSistem.LoadTests
{
    public sealed class LoadTestSeeder
    {
        public const string Password = "Lozinka123";
        public const string BankerEmail = "load.bankar@load.local";
        public const string AdminEmail = "load.admin@load.local";

        private readonly string _connectionString;
        private readonly BankApiClient _api;

        public LoadTestSeeder(string connectionString, BankApiClient api)
        {
            _connectionString = connectionString;
            _api = api;
        }

        public static string CustomerEmail(int index) => $"klijent{index:D5}@load.local";

        public async Task SeedAsync(int customers, decimal openingBalance)
        {
            var hash = BCrypt.Net.BCrypt.HashPassword(Password, 12);

            await using var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync();

            await ExecuteAsync(connection, $@"
                INSERT INTO users (FirstName, LastName, Email, PasswordHash, Role, KycStatus, CreatedAt, UpdatedAt) VALUES
                ('Load', 'Bankar', '{BankerEmail}', '{hash}', 'Bankar', 'NijePrimenljivo', UTC_TIMESTAMP(), UTC_TIMESTAMP()),
                ('Load', 'Admin', '{AdminEmail}', '{hash}', 'Administrator', 'NijePrimenljivo', UTC_TIMESTAMP(), UTC_TIMESTAMP())");

            for (var batchStart = 0; batchStart < customers; batchStart += 500)
            {
                var values = new StringBuilder();
                for (var index = batchStart; index < Math.Min(customers, batchStart + 500); index++)
                {
                    if (values.Length > 0)
                        values.Append(',');
                    values.Append($"('Klijent', 'Broj{index}', '{CustomerEmail(index)}', '{hash}', 'Korisnik', 'Verifikovan', UTC_TIMESTAMP(), UTC_TIMESTAMP(), UTC_TIMESTAMP())");
                }

                await ExecuteAsync(connection,
                    "INSERT INTO users (FirstName, LastName, Email, PasswordHash, Role, KycStatus, KycVerifiedAt, CreatedAt, UpdatedAt) VALUES " + values);
            }

            await ExecuteAsync(connection, @"
                INSERT INTO user_consents (UserId, LegalDocumentId, AcceptedAt)
                SELECT u.Id, d.Id, UTC_TIMESTAMP() FROM users u CROSS JOIN legal_documents d WHERE u.Email LIKE '%@load.local'");

            var banker = await _api.LoginAsync(BankerEmail, Password);
            var bankerToken = banker.Result?.Token ?? throw new InvalidOperationException("Prijava bankara nije uspela.");

            var customerIds = new List<int>();
            await using (var command = new MySqlCommand("SELECT Id FROM users WHERE Email LIKE 'klijent%@load.local' ORDER BY Email", connection))
            await using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                    customerIds.Add(reader.GetInt32(0));
            }

            using var throttle = new SemaphoreSlim(32);
            await Task.WhenAll(customerIds.Select(async customerId =>
            {
                await throttle.WaitAsync();
                try
                {
                    var (status, _, body) = await _api.SendAsync(HttpMethod.Post, "/api/v1/accounts", bankerToken,
                        new { userId = customerId, currency = "RSD" }, readBody: true);
                    if (status != 201)
                        throw new InvalidOperationException($"Otvaranje računa nije uspelo: {status} {body}");

                    var accountId = System.Text.Json.JsonDocument.Parse(body).RootElement.GetProperty("id").GetInt32();
                    var (depositStatus, _, depositBody) = await _api.SendAsync(HttpMethod.Post, $"/api/v1/accounts/{accountId}/deposits", bankerToken,
                        new { amount = openingBalance, description = "Pocetni depozit" });
                    if (depositStatus != 200)
                        throw new InvalidOperationException($"Uplata nije uspela: {depositStatus} {depositBody}");
                }
                finally
                {
                    throttle.Release();
                }
            }));
        }

        private static async Task ExecuteAsync(MySqlConnection connection, string sql)
        {
            await using var command = new MySqlCommand(sql, connection) { CommandTimeout = 300 };
            await command.ExecuteNonQueryAsync();
        }
    }
}
