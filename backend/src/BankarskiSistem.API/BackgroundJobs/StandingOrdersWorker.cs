using BankarskiSistem.Core.Interfaces;

namespace BankarskiSistem.API.BackgroundJobs
{
    public class StandingOrdersOptions
    {
        public const string SectionName = "Jobs:StandingOrders";

        public bool Enabled { get; set; } = true;
        public int IntervalSeconds { get; set; } = 60;
        public int BatchSize { get; set; } = 50;
    }

    public class StandingOrdersWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly StandingOrdersOptions _options;
        private readonly ILogger<StandingOrdersWorker> _logger;

        public StandingOrdersWorker(
            IServiceScopeFactory scopeFactory,
            Microsoft.Extensions.Options.IOptions<StandingOrdersOptions> options,
            ILogger<StandingOrdersWorker> logger)
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
                    var standingOrders = scope.ServiceProvider.GetRequiredService<IStandingOrderService>();
                    var processed = await standingOrders.ExecuteDueAsync(_options.BatchSize, stoppingToken);
                    if (processed > 0)
                        _logger.LogInformation("Obrađeno trajnih naloga: {Count}", processed);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(exception, "Greška u obradi trajnih naloga");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
    }
}
