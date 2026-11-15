using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Settings;
using BankarskiSistem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Infrastructure.Services
{
    public class SessionService : ISessionService
    {
        private const int KnownDeviceLookbackDays = 90;

        private readonly ApplicationDbContext _context;
        private readonly IRequestContext _requestContext;
        private readonly IPasswordHasher _passwordHasher;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly ISessionValidationCache _sessionCache;
        private readonly CustomerProtectionSettings _settings;
        private readonly TimeProvider _timeProvider;

        public SessionService(
            ApplicationDbContext context,
            IRequestContext requestContext,
            IPasswordHasher passwordHasher,
            INotificationPublisher notificationPublisher,
            ISessionValidationCache sessionCache,
            IOptions<CustomerProtectionSettings> settings,
            TimeProvider timeProvider)
        {
            _context = context;
            _requestContext = requestContext;
            _passwordHasher = passwordHasher;
            _notificationPublisher = notificationPublisher;
            _sessionCache = sessionCache;
            _settings = settings.Value;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<UserSession> StartAsync(int userId, DateTime expiresAt)
        {
            var userAgent = Truncate(_requestContext.UserAgent, 255);
            var deviceLabel = DeviceLabelParser.Describe(userAgent);
            var lookback = UtcNow.AddDays(-KnownDeviceLookbackDays);

            var hadSessions = await _context.UserSessions.AnyAsync(s => s.UserId == userId);
            var knownDevice = await _context.UserSessions.AnyAsync(s =>
                s.UserId == userId && s.CreatedAt >= lookback && s.DeviceLabel == deviceLabel && s.IpAddress == _requestContext.IpAddress);

            var session = new UserSession
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CreatedAt = UtcNow,
                LastSeenAt = UtcNow,
                ExpiresAt = expiresAt,
                IpAddress = _requestContext.IpAddress,
                UserAgent = userAgent,
                DeviceLabel = deviceLabel
            };
            await _context.UserSessions.AddAsync(session);

            if (hadSessions && !knownDevice)
            {
                await _notificationPublisher.PublishAsync(new Notification
                {
                    UserId = userId,
                    Type = NotificationType.Bezbednost,
                    Title = "Prijava sa novog uređaja",
                    Message = $"Zabeležena je prijava na vaš nalog sa uređaja \"{deviceLabel}\" (IP {_requestContext.IpAddress ?? "nepoznata"}). " +
                              "Ako to niste bili vi, odjavite tu sesiju u odeljku Bezbednost i promenite lozinku.",
                    CreatedAt = UtcNow
                });
            }

            await _context.SaveChangesAsync();
            return session;
        }

        public async Task<bool> ValidateAndTouchAsync(Guid sessionId, int userId)
        {
            var session = await _context.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId);
            if (session == null || !session.IsActiveAt(UtcNow))
                return false;

            if ((UtcNow - session.LastSeenAt).TotalSeconds >= _settings.SessionTouchIntervalSeconds)
            {
                session.LastSeenAt = UtcNow;
                await _context.SaveChangesAsync();
            }
            return true;
        }

        public async Task<IReadOnlyList<UserSession>> GetActiveAsync(int userId)
        {
            var now = UtcNow;
            return await _context.UserSessions.AsNoTracking()
                .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
                .OrderByDescending(s => s.LastSeenAt)
                .ToListAsync();
        }

        public async Task RevokeAsync(int userId, Guid sessionId, string reason)
        {
            var session = await _context.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId)
                ?? throw new NotFoundException("Sesija nije pronađena.");
            session.Revoke(reason, UtcNow);
            await _context.SaveChangesAsync();
            _sessionCache.InvalidateSession(sessionId);
        }

        public async Task<int> RevokeOthersAsync(int userId, Guid currentSessionId, string reason)
        {
            var sessions = await _context.UserSessions
                .Where(s => s.UserId == userId && s.Id != currentSessionId && s.RevokedAt == null)
                .ToListAsync();
            foreach (var session in sessions)
                session.Revoke(reason, UtcNow);
            await _context.SaveChangesAsync();
            _sessionCache.InvalidateUser(userId);
            return sessions.Count;
        }

        public async Task<int> RevokeAllAsync(int userId, string reason)
        {
            var sessions = await _context.UserSessions.Where(s => s.UserId == userId && s.RevokedAt == null).ToListAsync();
            foreach (var session in sessions)
                session.Revoke(reason, UtcNow);
            await _context.SaveChangesAsync();
            _sessionCache.InvalidateUser(userId);
            return sessions.Count;
        }

        public async Task ReauthenticateAsync(int userId, Guid sessionId, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId)
                ?? throw new NotFoundException("Korisnik nije pronađen.");
            var session = await _context.UserSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId)
                ?? throw new NotFoundException("Sesija nije pronađena.");

            if (!_passwordHasher.Verify(password, user.PasswordHash))
                throw new BusinessRuleException("Lozinka nije ispravna.");

            session.ReauthenticatedAt = UtcNow;
            await _context.SaveChangesAsync();
        }

        public async Task<bool> IsRecentlyReauthenticatedAsync(Guid sessionId)
        {
            var threshold = UtcNow.AddMinutes(-_settings.ReauthenticationMinutes);
            return await _context.UserSessions.AnyAsync(s =>
                s.Id == sessionId && s.RevokedAt == null && s.ReauthenticatedAt != null && s.ReauthenticatedAt >= threshold);
        }

        private static string? Truncate(string? value, int length) =>
            value is { Length: > 0 } && value.Length > length ? value[..length] : value;
    }

    public static class DeviceLabelParser
    {
        public static string Describe(string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(userAgent))
                return "Nepoznat uređaj";

            var browser = userAgent switch
            {
                var ua when ua.Contains("Edg/") => "Edge",
                var ua when ua.Contains("OPR/") => "Opera",
                var ua when ua.Contains("Firefox/") => "Firefox",
                var ua when ua.Contains("Chrome/") => "Chrome",
                var ua when ua.Contains("Safari/") => "Safari",
                var ua when ua.Contains("python", StringComparison.OrdinalIgnoreCase) => "Skripta",
                _ => "Pregledač"
            };

            var system = userAgent switch
            {
                var ua when ua.Contains("Windows") => "Windows",
                var ua when ua.Contains("Android") => "Android",
                var ua when ua.Contains("iPhone") || ua.Contains("iPad") => "iOS",
                var ua when ua.Contains("Mac OS") => "macOS",
                var ua when ua.Contains("Linux") => "Linux",
                _ => "nepoznat sistem"
            };

            return $"{browser} na {system}";
        }
    }
}
