using System.Collections.Concurrent;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Settings;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Infrastructure.Security
{
    public class SessionValidationCache : ISessionValidationCache
    {
        private const int PruneThreshold = 20_000;

        private readonly ConcurrentDictionary<Guid, CachedSession> _sessions = new();
        private readonly ConcurrentDictionary<int, int> _userGenerations = new();
        private readonly TimeSpan _timeToLive;
        private readonly TimeProvider _timeProvider;

        public SessionValidationCache(IOptions<CustomerProtectionSettings> settings, TimeProvider timeProvider)
        {
            _timeToLive = TimeSpan.FromSeconds(Math.Max(0, settings.Value.SessionCacheSeconds));
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public bool IsKnownValid(Guid sessionId, int userId, int tokenVersion, string role)
        {
            if (!_sessions.TryGetValue(sessionId, out var cached))
                return false;

            var isValid = cached.UserId == userId
                          && cached.TokenVersion == tokenVersion
                          && cached.Role == role
                          && cached.ValidUntil > UtcNow
                          && cached.Generation == GenerationOf(userId);

            if (!isValid)
                _sessions.TryRemove(sessionId, out _);

            return isValid;
        }

        public void Remember(Guid sessionId, int userId, int tokenVersion, string role, DateTime sessionExpiresAt)
        {
            if (_timeToLive == TimeSpan.Zero)
                return;

            if (_sessions.Count > PruneThreshold)
                Prune();

            var validUntil = UtcNow.Add(_timeToLive);
            if (sessionExpiresAt < validUntil)
                validUntil = sessionExpiresAt;

            _sessions[sessionId] = new CachedSession(userId, tokenVersion, role, validUntil, GenerationOf(userId));
        }

        public void InvalidateSession(Guid sessionId) => _sessions.TryRemove(sessionId, out _);

        public void InvalidateUser(int userId) => _userGenerations.AddOrUpdate(userId, 1, (_, generation) => generation + 1);

        private int GenerationOf(int userId) => _userGenerations.TryGetValue(userId, out var generation) ? generation : 0;

        private void Prune()
        {
            var now = UtcNow;
            foreach (var (sessionId, cached) in _sessions)
            {
                if (cached.ValidUntil <= now)
                    _sessions.TryRemove(sessionId, out _);
            }
        }

        private sealed record CachedSession(int UserId, int TokenVersion, string Role, DateTime ValidUntil, int Generation);
    }
}
