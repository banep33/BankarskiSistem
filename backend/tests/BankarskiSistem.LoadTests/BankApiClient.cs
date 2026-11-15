using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BankarskiSistem.LoadTests
{
    public sealed record LoginResult(string Token, int UserId);

    public sealed record AccountInfo(int Id, string AccountNumber, decimal Balance);

    public sealed class BankApiClient
    {
        public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            Converters = { new JsonStringEnumConverter() }
        };

        private readonly HttpClient _http;

        public BankApiClient(string baseUrl)
        {
            var handler = new SocketsHttpHandler
            {
                MaxConnectionsPerServer = 2000,
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };
            _http = new HttpClient(handler) { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(60) };
        }

        public async Task<(LoginResult? Result, int Status, double Milliseconds)> LoginAsync(string email, string password)
        {
            var (response, ms) = await Timing.MeasureAsync(() =>
                _http.PostAsJsonAsync("/api/v1/auth/login", new { email, password }, JsonOptions));
            using (response)
            {
                if (!response.IsSuccessStatusCode)
                    return (null, (int)response.StatusCode, ms);

                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var root = document.RootElement;
                return (new LoginResult(root.GetProperty("token").GetString()!, root.GetProperty("id").GetInt32()), 200, ms);
            }
        }

        public async Task<(int Status, double Milliseconds, string Body)> SendAsync(
            HttpMethod method, string path, string token, object? body = null, string? idempotencyKey = null, bool readBody = false)
        {
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (idempotencyKey != null)
                request.Headers.Add("Idempotency-Key", idempotencyKey);
            if (body != null)
                request.Content = JsonContent.Create(body, options: JsonOptions);

            try
            {
                var (response, ms) = await Timing.MeasureAsync(() => _http.SendAsync(request));
                using (response)
                {
                    var content = readBody || !response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync() : string.Empty;
                    return ((int)response.StatusCode, ms, content);
                }
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                return (0, 0, exception.Message);
            }
        }

        public async Task<IReadOnlyList<AccountInfo>> GetMyAccountsAsync(string token)
        {
            var (status, _, body) = await SendAsync(HttpMethod.Get, "/api/v1/me/accounts", token, readBody: true);
            if (status != 200)
                throw new InvalidOperationException($"Ne mogu da učitam račune: {status} {body}");
            return JsonSerializer.Deserialize<List<AccountInfo>>(body, JsonOptions)!;
        }
    }
}
