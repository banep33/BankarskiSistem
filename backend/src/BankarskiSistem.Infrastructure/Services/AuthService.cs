using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Core.Settings;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private static readonly Lazy<string> TimingEqualizationHash =
            new(() => BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), 12));

        private readonly IUserRepository _userRepository;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly ISessionService _sessionService;
        private readonly ISessionValidationCache _sessionCache;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IPasswordHasher _passwordHasher;
        private readonly ITokenService _tokenService;
        private readonly SecuritySettings _securitySettings;
        private readonly TimeProvider _timeProvider;

        public AuthService(
            IUserRepository userRepository,
            INotificationPublisher notificationPublisher,
            ISessionService sessionService,
            ISessionValidationCache sessionCache,
            IUnitOfWork unitOfWork,
            IPasswordHasher passwordHasher,
            ITokenService tokenService,
            IOptions<SecuritySettings> securitySettings,
            TimeProvider timeProvider)
        {
            _userRepository = userRepository;
            _notificationPublisher = notificationPublisher;
            _sessionService = sessionService;
            _sessionCache = sessionCache;
            _unitOfWork = unitOfWork;
            _passwordHasher = passwordHasher;
            _tokenService = tokenService;
            _securitySettings = securitySettings.Value;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<AuthResult> LoginAsync(string email, string password)
        {
            var user = await _userRepository.GetByEmailAsync(EmailNormalizer.Normalize(email));

            if (user == null)
            {
                _passwordHasher.Verify(password, TimingEqualizationHash.Value);
                throw new InvalidCredentialsException();
            }

            if (user.IsLocked && user.LockedUntil > UtcNow)
                throw new AccountLockedException(RemainingLockMinutes(user)) { UserId = user.Id };

            if (user.IsLocked)
                ResetLockout(user);

            if (!_passwordHasher.Verify(password, user.PasswordHash))
            {
                await RegisterFailedAttemptAsync(user);
                if (user.IsLocked)
                    throw new AccountLockedException(RemainingLockMinutes(user)) { UserId = user.Id };
                throw new InvalidCredentialsException { UserId = user.Id };
            }

            if (!user.IsActive)
                throw new AccountDeactivatedException { UserId = user.Id };

            ResetLockout(user);
            await _unitOfWork.SaveChangesAsync();

            return await IssueAsync(user);
        }

        public async Task<AuthResult> ChangePasswordAsync(int userId, Guid currentSessionId, string currentPassword, string newPassword)
        {
            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new NotFoundException("Korisnik nije pronađen.");

            if (!_passwordHasher.Verify(currentPassword, user.PasswordHash))
                throw new BusinessRuleException("Trenutna lozinka nije ispravna.");

            user.PasswordHash = _passwordHasher.Hash(newPassword);
            user.InvalidateTokens();
            user.UpdatedAt = UtcNow;
            await _notificationPublisher.PublishAsync(NotificationFactory.PasswordChanged(user, UtcNow));
            await _unitOfWork.SaveChangesAsync();
            await _sessionService.RevokeAllAsync(userId, "Promena lozinke");

            return await IssueAsync(user);
        }

        public async Task<bool> IsSessionValidAsync(int userId, int tokenVersion, string role, Guid sessionId)
        {
            if (_sessionCache.IsKnownValid(sessionId, userId, tokenVersion, role))
                return true;

            var user = await _userRepository.GetByIdAsync(userId);

            var userIsValid = user != null
                              && user.IsActive
                              && user.TokenVersion == tokenVersion
                              && user.Role.ToString() == role
                              && !(user.IsLocked && user.LockedUntil > UtcNow);

            if (!userIsValid || !await _sessionService.ValidateAndTouchAsync(sessionId, userId))
                return false;

            _sessionCache.Remember(sessionId, userId, tokenVersion, role, UtcNow.AddMinutes(30));
            return true;
        }

        public Task LogoutAsync(int userId, Guid sessionId) =>
            _sessionService.RevokeAsync(userId, sessionId, "Odjava");

        private async Task<AuthResult> IssueAsync(User user)
        {
            var expiresAt = _tokenService.NextExpiry();
            var session = await _sessionService.StartAsync(user.Id, expiresAt);
            var token = _tokenService.CreateToken(user, session.Id, expiresAt);
            return new AuthResult(user, token, expiresAt, session.Id);
        }

        private async Task RegisterFailedAttemptAsync(User user)
        {
            user.FailedLoginAttempts++;
            user.UpdatedAt = UtcNow;

            if (user.FailedLoginAttempts >= _securitySettings.MaxFailedLoginAttempts)
            {
                user.IsLocked = true;
                user.LockedUntil = UtcNow.AddMinutes(_securitySettings.LockoutDurationMinutes);
                user.InvalidateTokens();
                _sessionCache.InvalidateUser(user.Id);
                await _notificationPublisher.PublishAsync(NotificationFactory.LoginLocked(user, UtcNow));
            }

            await _unitOfWork.SaveChangesAsync();
        }

        private void ResetLockout(User user)
        {
            user.FailedLoginAttempts = 0;
            user.IsLocked = false;
            user.LockedUntil = null;
            user.UpdatedAt = UtcNow;
        }

        private int RemainingLockMinutes(User user) =>
            Math.Max(1, (int)Math.Ceiling((user.LockedUntil!.Value - UtcNow).TotalMinutes));
    }
}
