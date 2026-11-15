using BankarskiSistem.Core.Interfaces;

namespace BankarskiSistem.API.BackgroundJobs
{
    public class ScheduledPaymentsOptions
    {
        public const string SectionName = "Jobs:ScheduledPayments";

        public bool Enabled { get; set; } = true;
        public int IntervalSeconds { get; set; } = 60;
        public int BatchSize { get; set; } = 50;
    }

    public class ScheduledPaymentsWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ScheduledPaymentsOptions _options;
        private readonly ILogger<ScheduledPaymentsWorker> _logger;

        public ScheduledPaymentsWorker(
            IServiceScopeFactory scopeFactory,
            Microsoft.Extensions.Options.IOptions<ScheduledPaymentsOptions> options,
            ILogger<ScheduledPaymentsWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Enabled)
                return;

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.IntervalSeconds)));
            do
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var orders = scope.ServiceProvider.GetRequiredService<IPaymentOrderService>();
                    var processed = await orders.ExecuteDueOrdersAsync(_options.BatchSize, stoppingToken);
                    if (processed > 0)
                        _logger.LogInformation("Obrađeno zakazanih naloga: {Count}", processed);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(exception, "Greška u obradi zakazanih naloga");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
