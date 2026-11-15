using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using Microsoft.Extensions.Logging;

namespace BankarskiSistem.Infrastructure.Services
{
    public class AuditLogService : IAuditLogService
    {
        private const int MaxTextLength = 2000;

        private readonly IAuditLogRepository _auditLogRepository;
        private readonly IRequestContext _requestContext;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<AuditLogService> _logger;

        public AuditLogService(
            IAuditLogRepository auditLogRepository,
            IRequestContext requestContext,
            TimeProvider timeProvider,
            ILogger<AuditLogService> logger)
        {
            _auditLogRepository = auditLogRepository;
            _requestContext = requestContext;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task LogAsync(AuditEntry entry)
        {
            try
            {
                await _auditLogRepository.AddAsync(new AuditLog
                {
                    UserId = entry.UserId,
                    Action = Truncate(entry.Action, 255)!,
                    EntityType = entry.EntityType,
                    EntityId = entry.EntityId,
                    IpAddress = _requestContext.IpAddress,
                    OldValue = Truncate(entry.OldValue, MaxTextLength),
                    NewValue = Truncate(entry.NewValue, MaxTextLength),
                    IsSuccess = entry.IsSuccess,
                    ErrorMessage = Truncate(entry.ErrorMessage, MaxTextLength),
                    CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
                });
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Upis u audit log nije uspeo za akciju {Action}", entry.Action);
            }
        }

        public Task<PagedResult<AuditLog>> GetPagedAsync(AuditLogFilter filter, PageRequest page) =>
            _auditLogRepository.GetPagedAsync(filter, page);

        private static string? Truncate(string? value, int maxLength) =>
            value is { Length: > 0 } && value.Length > maxLength ? value[..maxLength] : value;
    }
}
