using System.Text;
using BankarskiSistem.Core.Settings;
using BankarskiSistem.Infrastructure;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Repositories;
using BankarskiSistem.Infrastructure.Security;
using BankarskiSistem.Infrastructure.Services;
using BankarskiSistem.Infrastructure.Services.Payments;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MySqlConnector;

namespace BankarskiSistem.Tests.Integration
{
    public sealed class MySqlFactAttribute : FactAttribute
    {
        public MySqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(MySqlDatabaseFixture.ServerConnectionString))
                Skip = $"Postavite promenljivu okruzenja {MySqlDatabaseFixture.ConnectionVariable} za integracione testove.";
        }
    }

    public class MySqlDatabaseFixture : IAsyncLifetime
    {
        public const string ConnectionVariable = "BANKARSKI_TEST_MYSQL";
        private const string DatabaseName = "bankarskisistemdb_integration_tests";

        public static string? ServerConnectionString => Environment.GetEnvironmentVariable(ConnectionVariable);

        public string ConnectionString { get; private set; } = string.Empty;

        public async Task InitializeAsync()
        {
            if (string.IsNullOrWhiteSpace(ServerConnectionString))
                return;

            var builder = new MySqlConnectionStringBuilder(ServerConnectionString) { AllowUserVariables = true };
            await using (var server = new MySqlConnection(builder.ConnectionString))
            {
                await server.OpenAsync();
                await ExecuteAsync(server, $"DROP DATABASE IF EXISTS `{DatabaseName}`");
                await ExecuteAsync(server, $"CREATE DATABASE `{DatabaseName}` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci");
            }

            builder.Database = DatabaseName;
            ConnectionString = builder.ConnectionString;

            await using var connection = new MySqlConnection(ConnectionString);
            await connection.OpenAsync();
            foreach (var script in Directory.GetFiles(FindMigrationsDirectory(), "V*.sql").Order())
            {
                foreach (var statement in SplitScript(await File.ReadAllTextAsync(script)))
                    await ExecuteAsync(connection, statement);
            }
        }

        public async Task DisposeAsync()
        {
            if (string.IsNullOrWhiteSpace(ServerConnectionString))
                return;

            await using var server = new MySqlConnection(ServerConnectionString);
            await server.OpenAsync();
            await ExecuteAsync(server, $"DROP DATABASE IF EXISTS `{DatabaseName}`");
        }

        public ApplicationDbContext CreateContext() =>
            new(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseMySql(ConnectionString, DependencyInjection.DefaultServerVersion)
                .Options);

        public PaymentService CreatePaymentService(ApplicationDbContext context, SecuritySettings? settings = null) =>
            new IntegrationServices(context, settings).Payments;

        public PaymentOrderService CreatePaymentOrderService(ApplicationDbContext context, SecuritySettings? settings = null) =>
            new IntegrationServices(context, settings).PaymentOrders;

        public static AccountNumberGenerator CreateAccountNumberGenerator() =>
            new(Options.Create(new BankingSettings()));

        internal static IEnumerable<string> SplitScript(string script)
        {
            var delimiter = ";";
            var current = new StringBuilder();

            foreach (var rawLine in script.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.TrimStart().StartsWith("DELIMITER ", StringComparison.OrdinalIgnoreCase))
                {
                    delimiter = line.Trim()["DELIMITER ".Length..].Trim();
                    continue;
                }

                current.AppendLine(line);
                if (line.TrimEnd().EndsWith(delimiter, StringComparison.Ordinal))
                {
                    var statement = current.ToString().TrimEnd();
                    statement = statement[..^delimiter.Length].Trim();
                    if (statement.Length > 0)
                        yield return statement;
                    current.Clear();
                }
            }

            var remainder = current.ToString().Trim();
            if (remainder.Length > 0)
                yield return remainder;
        }

        private static async Task ExecuteAsync(MySqlConnection connection, string sql)
        {
            await using var command = new MySqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }

        private static string FindMigrationsDirectory()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "database", "migrations");
                if (Directory.Exists(candidate))
                    return candidate;
                directory = directory.Parent;
            }
            throw new DirectoryNotFoundException("Folder database/migrations nije pronadjen.");
        }
    }

    [CollectionDefinition(Name)]
    public class MySqlCollection : ICollectionFixture<MySqlDatabaseFixture>
    {
        public const string Name = "MySql";
    }
}
