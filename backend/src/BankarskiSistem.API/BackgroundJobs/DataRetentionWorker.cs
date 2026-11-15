using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.API.BackgroundJobs
{
    public class DataRetentionOptions
    {
        public const string SectionName = "Jobs:DataRetention";

        public bool Enabled { get; set; } = true;
        public int IntervalHours { get; set; } = 24;
    }

    public class SupportContactOptions
    {
        public const string SectionName = "Support";

        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string WorkingHours { get; set; } = string.Empty;
        public string RegulatorInfo { get; set; } = string.Empty;
    }

    public class DataRetentionWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly DataRetentionOptions _options;
        private readonly ILogger<DataRetentionWorker> _logger;

        public DataRetentionWorker(
            IServiceScopeFactory scopeFactory,
            IOptions<DataRetentionOptions> options,
            ILogger<DataRetentionWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
                return;

            using var timer = new PeriodicTimer(TimeSpan.FromHours(Math.Max(1, _options.IntervalHours)));
            do
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var retention = scope.ServiceProvider.GetRequiredService<IDataRetentionService>();
                    var report = await retention.PurgeExpiredAsync(stoppingToken);

                    var auditLog = scope.ServiceProvider.GetRequiredService<IAuditLogService>();
                    await auditLog.LogAsync(new AuditEntry(null, "Automatsko ciscenje po rokovima cuvanja", "Retention",
                        NewValue: $"Obavestenja: {report.ReadNotificationsDeleted}, sesije: {report.ExpiredSessionsDeleted}"));
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(exception, "Greška pri čišćenju podataka po rokovima čuvanja");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
