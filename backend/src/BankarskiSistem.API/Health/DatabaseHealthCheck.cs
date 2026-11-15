using BankarskiSistem.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace BankarskiSistem.API.Health
{
    public class DatabaseHealthCheck : IHealthCheck
    {
        public const string ReadyTag = "ready";

        private readonly ApplicationDbContext _context;

        public DatabaseHealthCheck(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            try
            {
                return await _context.Database.CanConnectAsync(cancellationToken)
                    ? HealthCheckResult.Healthy("Baza je dostupna.")
                    : HealthCheckResult.Unhealthy("Baza nije dostupna.");
            }
            catch (Exception exception)
            {
                return HealthCheckResult.Unhealthy("Baza nije dostupna.", exception);
            }
        }
    }
}
