using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.Tests.Fakes
{
    public class FakeTimeProvider : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 14, 10, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => Now;

        public void Advance(TimeSpan by) => Now = Now.Add(by);
    }

    public class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }

        public async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            var result = await operation();
            SaveCount++;
            return result;
        }
    }

    public class FakePasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hashed:" + password;

        public bool Verify(string password, string passwordHash) => passwordHash == "hashed:" + password;
    }

    public class FakeTokenService : ITokenService
    {
        public DateTime NextExpiry() => DateTime.UtcNow.AddMinutes(15);

        public DateTime NextSessionExpiry() => DateTime.UtcNow.AddHours(8);

        public string CreateToken(User user, Guid sessionId, DateTime expiresAt) => $"token-{user.Id}-v{user.TokenVersion}-{sessionId}";

        public string CreateTwoFactorChallenge(User user, DateTime expiresAt) => $"challenge:{user.Id}:{user.TokenVersion}";

        public (int UserId, int TokenVersion)? ReadTwoFactorChallenge(string token)
        {
            var parts = token.Split(':');
            return parts.Length == 3 && parts[0] == "challenge" ? (int.Parse(parts[1]), int.Parse(parts[2])) : null;
        }
    }

    public class FakeTwoFactorService : ITwoFactorService
    {
        public string ValidCode { get; set; } = "123456";

        public Task<TwoFactorStatus> GetStatusAsync(int userId) => Task.FromResult(new TwoFactorStatus(false, null, 0));
        public Task<TwoFactorSetup> BeginSetupAsync(int userId) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> EnableAsync(int userId, string code) => throw new NotSupportedException();
        public Task DisableAsync(int userId, string code) => throw new NotSupportedException();
        public Task<IReadOnlyList<string>> RegenerateRecoveryCodesAsync(int userId, string code) => throw new NotSupportedException();
        public Task<bool> VerifyLoginCodeAsync(User user, string code) => Task.FromResult(user.TwoFactorEnabled && code == ValidCode);
        public Task ResetByAdministratorAsync(int userId, Actor actor) => throw new NotSupportedException();
    }

    public class PlainSecretProtector : ISecretProtector
    {
        public string Protect(string plaintext) => $"zasticeno:{plaintext}";
        public string Unprotect(string protectedValue) => protectedValue["zasticeno:".Length..];
    }

    public class NoSessionCache : ISessionValidationCache
    {
        public bool IsKnownValid(Guid sessionId, int userId, int tokenVersion, string role) => false;
        public void Remember(Guid sessionId, int userId, int tokenVersion, string role, DateTime sessionExpiresAt) { }
        public void InvalidateSession(Guid sessionId) { }
        public void InvalidateUser(int userId) { }
    }

    public class FakeNotificationPublisher : INotificationPublisher
    {
        public List<Notification> Published { get; } = [];

        public Task PublishAsync(params Notification[] notifications)
        {
            Published.AddRange(notifications);
            return Task.CompletedTask;
        }
    }

    public class FakeSessionService : ISessionService
    {
        public List<UserSession> Sessions { get; } = [];

        public Task<UserSession> StartAsync(int userId, DateTime expiresAt, string refreshTokenHash)
        {
            var session = new UserSession { Id = Guid.NewGuid(), UserId = userId, CreatedAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow, ExpiresAt = expiresAt, RefreshTokenHash = refreshTokenHash };
            Sessions.Add(session);
            return Task.FromResult(session);
        }

        public Task<RefreshRotation> RotateRefreshTokenAsync(string presentedHash, string newHash)
        {
            var session = Sessions.SingleOrDefault(s => s.RefreshTokenHash == presentedHash && s.RevokedAt == null);
            if (session == null)
                return Task.FromResult(new RefreshRotation(RefreshRotationOutcome.Invalid, null));
            session.PreviousRefreshTokenHash = presentedHash;
            session.RefreshTokenHash = newHash;
            return Task.FromResult(new RefreshRotation(RefreshRotationOutcome.Rotated, session));
        }

        public Task<bool> ValidateAndTouchAsync(Guid sessionId, int userId) =>
            Task.FromResult(Sessions.Any(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null));

        public Task<IReadOnlyList<UserSession>> GetActiveAsync(int userId) =>
            Task.FromResult<IReadOnlyList<UserSession>>(Sessions.Where(s => s.UserId == userId && s.RevokedAt == null).ToList());

        public Task RevokeAsync(int userId, Guid sessionId, string reason)
        {
            Sessions.Single(s => s.Id == sessionId).Revoke(reason, DateTime.UtcNow);
            return Task.CompletedTask;
        }

        public Task<int> RevokeOthersAsync(int userId, Guid currentSessionId, string reason)
        {
            var others = Sessions.Where(s => s.UserId == userId && s.Id != currentSessionId && s.RevokedAt == null).ToList();
            others.ForEach(s => s.Revoke(reason, DateTime.UtcNow));
            return Task.FromResult(others.Count);
        }

        public Task<int> RevokeAllAsync(int userId, string reason) => RevokeOthersAsync(userId, Guid.Empty, reason);

        public Task ReauthenticateAsync(int userId, Guid sessionId, string password) => Task.CompletedTask;

        public Task<bool> IsRecentlyReauthenticatedAsync(Guid sessionId) => Task.FromResult(false);
    }

    public class InMemoryNotificationRepository : INotificationRepository
    {
        public List<Notification> Added { get; } = [];

        public Task<PagedResult<Notification>> GetPagedAsync(int userId, bool onlyUnread, PageRequest page) =>
            Task.FromResult(new PagedResult<Notification> { Items = Added.Where(n => n.UserId == userId).ToList() });

        public Task<int> CountUnreadAsync(int userId) => Task.FromResult(Added.Count(n => n.UserId == userId && !n.IsRead));

        public Task<Notification?> GetAsync(int userId, int notificationId) =>
            Task.FromResult(Added.FirstOrDefault(n => n.UserId == userId && n.Id == notificationId));

        public Task<int> MarkAllAsReadAsync(int userId) => Task.FromResult(0);

        public Task AddRangeAsync(IEnumerable<Notification> notifications)
        {
            Added.AddRange(notifications);
            return Task.CompletedTask;
        }
    }

    public class InMemoryUserRepository : IUserRepository
    {
        public List<User> Users { get; } = [];
        public HashSet<int> UsersWithHistory { get; } = [];

        public Task<User?> GetByIdAsync(int id) => Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByEmailAsync(string normalizedEmail) =>
            Task.FromResult(Users.FirstOrDefault(u => u.Email == normalizedEmail));

        public Task<bool> EmailExistsAsync(string normalizedEmail, int? excludeUserId = null) =>
            Task.FromResult(Users.Any(u => u.Email == normalizedEmail && u.Id != excludeUserId));

        public Task<bool> AnyAdministratorAsync() =>
            Task.FromResult(Users.Any(u => u.Role == Core.Enums.UserRole.Administrator));

        public Task<bool> HasFinancialHistoryAsync(int userId) => Task.FromResult(UsersWithHistory.Contains(userId));

        public Task<PagedResult<User>> GetPagedAsync(UserFilter filter, PageRequest page)
        {
            var items = Users.Where(u => filter.Role == null || u.Role == filter.Role).ToList();
            return Task.FromResult(new PagedResult<User>
            {
                Items = items, TotalCount = items.Count, Page = page.Page, PageSize = page.PageSize
            });
        }

        public Task AddAsync(User user)
        {
            user.Id = Users.Count == 0 ? 1 : Users.Max(u => u.Id) + 1;
            Users.Add(user);
            return Task.CompletedTask;
        }

        public void Remove(User user) => Users.Remove(user);
    }
}
