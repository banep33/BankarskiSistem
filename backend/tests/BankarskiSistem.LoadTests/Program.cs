using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using BankarskiSistem.LoadTests;

var options = LoadTestOptions.Parse(args);
var api = new BankApiClient(options.BaseUrl);
var report = new StringBuilder();
report.AppendLine($"# Load test — {DateTime.Now:yyyy-MM-dd HH:mm}");
report.AppendLine();
report.AppendLine($"Oznaka: **{options.Label}**, virtualnih korisnika: **{options.Users}**, trajanje scenarija: **{options.DurationSeconds} s**, adresa: `{options.BaseUrl}`");
report.AppendLine();

if (options.Seed)
{
    Console.WriteLine($"Punjenje podataka: {options.Users} klijenata...");
    var seedTimer = Stopwatch.StartNew();
    await new LoadTestSeeder(options.ConnectionString, api).SeedAsync(options.Users, 1_000_000m);
    Console.WriteLine($"Podaci napunjeni za {seedTimer.Elapsed.TotalSeconds:0.0} s");
}

var sessions = new VirtualUser[options.Users];
var login = new ScenarioResult("Talas prijava (svi korisnici istovremeno)", options.Users);
var loginTimer = Stopwatch.StartNew();
await Task.WhenAll(Enumerable.Range(0, options.Users).Select(async index =>
{
    var (result, status, ms) = await api.LoginAsync(LoadTestSeeder.CustomerEmail(index), LoadTestSeeder.Password);
    login.Record("POST /auth/login", ms, status);
    if (result != null)
        sessions[index] = new VirtualUser(index, result.Token);
}));
login.Elapsed = loginTimer.Elapsed;
Print(login);

var users = sessions.Where(s => s != null).ToArray();
if (users.Length == 0)
    throw new InvalidOperationException("Nijedna prijava nije uspela.");

await Task.WhenAll(users.Select(async user =>
{
    var accounts = await api.GetMyAccountsAsync(user.Token);
    user.AccountId = accounts[0].Id;
    user.AccountNumber = accounts[0].AccountNumber;
}));

var read = await RunAsync("Mešano čitanje (dashboard, računi, transakcije, obaveštenja)", users, async (user, result, random) =>
{
    var step = random.Next(4);
    var (endpoint, path) = step switch
    {
        0 => ("GET /me/dashboard", "/api/v1/me/dashboard"),
        1 => ("GET /me/accounts", "/api/v1/me/accounts"),
        2 => ("GET /me/transactions", "/api/v1/me/transactions?limit=20"),
        _ => ("GET /me/notifications/unread-count", "/api/v1/me/notifications/unread-count")
    };
    var (status, ms, _) = await api.SendAsync(HttpMethod.Get, path, user.Token);
    result.Record(endpoint, ms, status);
});
Print(read);

var payments = await RunAsync("Plaćanja između nasumičnih klijenata", users, async (user, result, random) =>
{
    var recipient = users[random.Next(users.Length)];
    if (recipient == user)
        return;

    if (random.Next(5) == 0)
    {
        var (quoteStatus, quoteMs, _) = await api.SendAsync(HttpMethod.Post, "/api/v1/payments/quote", user.Token, PaymentBody(user, recipient, random));
        result.Record("POST /payments/quote", quoteMs, quoteStatus);
        return;
    }

    var (status, ms, _) = await api.SendAsync(HttpMethod.Post, "/api/v1/payments", user.Token,
        PaymentBody(user, recipient, random), Guid.NewGuid().ToString("N"));
    result.Record("POST /payments", ms, status);
});
Print(payments);

var hotRecipient = users[0];
var hot = await RunAsync("Vruć račun (svi plaćaju istom primaocu)", users.Skip(1).ToArray(), async (user, result, random) =>
{
    var (status, ms, _) = await api.SendAsync(HttpMethod.Post, "/api/v1/payments", user.Token,
        PaymentBody(user, hotRecipient, random), Guid.NewGuid().ToString("N"));
    result.Record("POST /payments (isti primalac)", ms, status);
}, options.DurationSeconds / 2);
Print(hot);

var admin = await api.LoginAsync(LoadTestSeeder.AdminEmail, LoadTestSeeder.Password);
var (_, _, trialBalance) = await api.SendAsync(HttpMethod.Get, "/api/v1/admin/ledger/trial-balance", admin.Result!.Token, readBody: true);
var (_, _, reconciliation) = await api.SendAsync(HttpMethod.Get, "/api/v1/admin/ledger/reconciliation?onlyDifferences=true", admin.Result!.Token, readBody: true);
var balanced = JsonDocument.Parse(trialBalance).RootElement.EnumerateArray().All(line => line.GetProperty("total").GetDecimal() == 0);
var noDifferences = JsonDocument.Parse(reconciliation).RootElement.GetArrayLength() == 0;

report.AppendLine("## Ispravnost posle opterećenja");
report.AppendLine();
report.AppendLine($"- Probni bilans jednak nuli: **{(balanced ? "DA" : "NE")}**");
report.AppendLine($"- Računi bez razlike između stanja i glavne knjige: **{(noDifferences ? "DA" : "NE")}**");
report.AppendLine();
Console.WriteLine($"Probni bilans 0: {balanced}, bez razlika: {noDifferences}");

if (options.ReportPath != null)
{
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.ReportPath))!);
    await File.AppendAllTextAsync(options.ReportPath, report.ToString());
    Console.WriteLine($"Izveštaj: {options.ReportPath}");
}

return balanced && noDifferences ? 0 : 1;

void Print(ScenarioResult result)
{
    var markdown = result.ToMarkdown();
    report.Append(markdown);
    Console.WriteLine(markdown);
}

object PaymentBody(VirtualUser from, VirtualUser to, Random random) => new
{
    fromAccountId = from.AccountId,
    recipientName = "Klijent opterecenje",
    recipientAccountNumber = to.AccountNumber,
    amount = Math.Round((decimal)(random.NextDouble() * 99 + 1), 2),
    paymentCode = "289",
    purpose = "Test opterecenja"
};

async Task<ScenarioResult> RunAsync(string name, VirtualUser[] participants, Func<VirtualUser, ScenarioResult, Random, Task> step, int? durationSeconds = null)
{
    var result = new ScenarioResult(name, participants.Length);
    var duration = TimeSpan.FromSeconds(durationSeconds ?? options.DurationSeconds);
    var timer = Stopwatch.StartNew();
    await Task.WhenAll(participants.Select(async (user, index) =>
    {
        var random = new Random(index * 7919 + 17);
        while (timer.Elapsed < duration)
            await step(user, result, random);
    }));
    result.Elapsed = timer.Elapsed;
    return result;
}

namespace BankarskiSistem.LoadTests
{
    public sealed class VirtualUser
    {
        public VirtualUser(int index, string token)
        {
            Index = index;
            Token = token;
        }

        public int Index { get; }
        public string Token { get; }
        public int AccountId { get; set; }
        public string AccountNumber { get; set; } = string.Empty;
    }

    public sealed record LoadTestOptions(
        string BaseUrl,
        string ConnectionString,
        int Users,
        int DurationSeconds,
        bool Seed,
        string? ReportPath,
        string Label)
    {
        public static LoadTestOptions Parse(string[] args)
        {
            string Value(string name, string fallback)
            {
                var index = Array.IndexOf(args, name);
                return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
            }

            return new LoadTestOptions(
                Value("--base-url", "http://localhost:5199"),
                Value("--db", Environment.GetEnvironmentVariable("BANKARSKI_LOAD_MYSQL") ?? string.Empty),
                int.Parse(Value("--users", "100"), CultureInfo.InvariantCulture),
                int.Parse(Value("--duration", "30"), CultureInfo.InvariantCulture),
                args.Contains("--seed"),
                args.Contains("--report") ? Value("--report", string.Empty) : null,
                Value("--label", "osnovno merenje"));
        }
    }
}
