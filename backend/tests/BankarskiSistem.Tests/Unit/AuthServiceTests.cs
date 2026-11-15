using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Settings;
using BankarskiSistem.Infrastructure.Services;
using BankarskiSistem.Tests.Fakes;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Tests.Unit
{
    public class AuthServiceTests
    {
        private readonly InMemoryUserRepository _users = new();
        private readonly FakeTimeProvider _time = new();
        private readonly FakeNotificationPublisher _notifications = new();
        private readonly FakeSessionService _sessions = new();
        private readonly FakeTwoFactorService _twoFactor = new();
        private readonly AuthService _service;
        private readonly User _customer;

        public AuthServiceTests()
        {
            var settings = Options.Create(new SecuritySettings { MaxFailedLoginAttempts = 3, LockoutDurationMinutes = 15 });
            _service = new AuthService(_users, _notifications, _sessions, new NoSessionCache(), new FakeUnitOfWork(), new FakePasswordHasher(), new FakeTokenService(), _twoFactor, settings, _time);

            _customer = new User
            {
                Email = "ana@test.local",
                PasswordHash = "hashed:Ispravna123",
                Role = UserRole.Korisnik,
                IsActive = true
            };
            _users.AddAsync(_customer).Wait();
        }

        [Fact]
        public async Task Login_WithValidCredentials_ReturnsTokenAndResetsCounter()
        {
            _customer.FailedLoginAttempts = 2;

            var result = (await _service.LoginAsync("  ANA@test.local ", "Ispravna123")).Session!;

            Assert.Equal(_customer.Id, result.User.Id);
            Assert.Equal(0, _customer.FailedLoginAttempts);
        }

        [Fact]
        public async Task Login_WithUnknownEmail_ThrowsGenericInvalidCredentials()
        {
            var unknown = await Assert.ThrowsAsync<InvalidCredentialsException>(
                () => _service.LoginAsync("nepoznat@test.local", "bilo-sta"));
            var wrongPassword = await Assert.ThrowsAsync<InvalidCredentialsException>(
                () => _service.LoginAsync(_customer.Email, "pogresna"));

            Assert.Equal(unknown.Message, wrongPassword.Message);
            Assert.Null(unknown.UserId);
            Assert.Equal(_customer.Id, wrongPassword.UserId);
        }

        [Fact]
        public async Task Login_AfterMaxFailedAttempts_LocksAccountAndInvalidatesTokens()
        {
            var versionBefore = _customer.TokenVersion;

            await Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync(_customer.Email, "pogresna"));
            await Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync(_customer.Email, "pogresna"));
            await Assert.ThrowsAsync<AccountLockedException>(() => _service.LoginAsync(_customer.Email, "pogresna"));

            Assert.True(_customer.IsLocked);
            Assert.True(_customer.TokenVersion > versionBefore);
            Assert.Contains(_notifications.Published, n => n.UserId == _customer.Id && n.Type == NotificationType.Bezbednost);
            await Assert.ThrowsAsync<AccountLockedException>(() => _service.LoginAsync(_customer.Email, "Ispravna123"));
        }

        [Fact]
        public async Task Login_AfterLockoutExpires_AllowsValidLogin()
        {
            _customer.IsLocked = true;
            _customer.LockedUntil = _time.GetUtcNow().UtcDateTime.AddMinutes(5);
            _time.Advance(TimeSpan.FromMinutes(6));

            var result = (await _service.LoginAsync(_customer.Email, "Ispravna123")).Session!;

            Assert.False(result.User.IsLocked);
        }

        [Fact]
        public async Task Login_DeactivatedUser_IsRejectedOnlyAfterPasswordCheck()
        {
            _customer.IsActive = false;

            await Assert.ThrowsAsync<InvalidCredentialsException>(() => _service.LoginAsync(_customer.Email, "pogresna"));
            await Assert.ThrowsAsync<AccountDeactivatedException>(() => _service.LoginAsync(_customer.Email, "Ispravna123"));
        }

        [Fact]
        public async Task ChangePassword_InvalidatesPreviousTokens()
        {
            var versionBefore = _customer.TokenVersion;

            var first = (await _service.LoginAsync(_customer.Email, "Ispravna123")).Session!;
            var second = (await _service.LoginAsync(_customer.Email, "Ispravna123")).Session!;

            var result = await _service.ChangePasswordAsync(_customer.Id, first.SessionId, "Ispravna123", "NovaLozinka1");

            Assert.Equal(versionBefore + 1, _customer.TokenVersion);
            Assert.False(await _service.IsSessionValidAsync(_customer.Id, versionBefore, "Korisnik", first.SessionId));
            Assert.False(await _service.IsSessionValidAsync(_customer.Id, result.User.TokenVersion, "Korisnik", second.SessionId));
            Assert.True(await _service.IsSessionValidAsync(_customer.Id, result.User.TokenVersion, "Korisnik", result.SessionId));
        }

        [Fact]
        public async Task IsSessionValid_FailsWhenRoleChanged()
        {
            var login = (await _service.LoginAsync(_customer.Email, "Ispravna123")).Session!;
            Assert.True(await _service.IsSessionValidAsync(_customer.Id, _customer.TokenVersion, "Korisnik", login.SessionId));
            Assert.False(await _service.IsSessionValidAsync(_customer.Id, _customer.TokenVersion, "Administrator", login.SessionId));
        }

        [Fact]
        public async Task Logout_RevokesOnlyCurrentSession()
        {
            var phone = (await _service.LoginAsync(_customer.Email, "Ispravna123")).Session!;
            var laptop = (await _service.LoginAsync(_customer.Email, "Ispravna123")).Session!;

            await _service.LogoutAsync(_customer.Id, phone.SessionId);

            Assert.False(await _service.IsSessionValidAsync(_customer.Id, _customer.TokenVersion, "Korisnik", phone.SessionId));
            Assert.True(await _service.IsSessionValidAsync(_customer.Id, _customer.TokenVersion, "Korisnik", laptop.SessionId));
        }

        [Fact]
        public async Task Login_WithTwoFactorEnabled_ReturnsChallengeUntilCodeIsVerified()
        {
            _customer.TotpEnabledAt = DateTime.UtcNow;
            _customer.TotpSecretProtected = "tajna";

            var outcome = await _service.LoginAsync(_customer.Email, "Ispravna123");

            Assert.Null(outcome.Session);
            Assert.NotNull(outcome.Challenge);
            Assert.Empty(_sessions.Sessions);

            await Assert.ThrowsAsync<InvalidTwoFactorCodeException>(() => _service.CompleteTwoFactorAsync(outcome.Challenge!.Token, "000000"));
            Assert.Equal(1, _customer.FailedLoginAttempts);

            var result = await _service.CompleteTwoFactorAsync(outcome.Challenge!.Token, _twoFactor.ValidCode);
            Assert.Equal(_customer.Id, result.User.Id);
            Assert.Equal(0, _customer.FailedLoginAttempts);
            Assert.NotNull(result.RefreshToken);
        }

        [Fact]
        public async Task TwoFactorChallenge_IsInvalidAfterPasswordChangeOrWrongCodesLockAccount()
        {
            _customer.TotpEnabledAt = DateTime.UtcNow;
            _customer.TotpSecretProtected = "tajna";
            var challenge = (await _service.LoginAsync(_customer.Email, "Ispravna123")).Challenge!;

            await Assert.ThrowsAsync<InvalidTwoFactorCodeException>(() => _service.CompleteTwoFactorAsync(challenge.Token, "111111"));
            await Assert.ThrowsAsync<InvalidTwoFactorCodeException>(() => _service.CompleteTwoFactorAsync(challenge.Token, "222222"));
            await Assert.ThrowsAsync<AccountLockedException>(() => _service.CompleteTwoFactorAsync(challenge.Token, "333333"));
            Assert.True(_customer.IsLocked);
            await Assert.ThrowsAsync<InvalidTwoFactorCodeException>(() => _service.CompleteTwoFactorAsync(challenge.Token, _twoFactor.ValidCode));
        }

        [Fact]
        public async Task Refresh_RotatesTokenAndRejectsOldOne()
        {
            var login = (await _service.LoginAsync(_customer.Email, "Ispravna123")).Session!;

            var refreshed = await _service.RefreshAsync(login.RefreshToken!);
            Assert.Equal(login.SessionId, refreshed.SessionId);
            Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);

            await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync(login.RefreshToken!));
            await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync(""));

            _customer.IsActive = false;
            await Assert.ThrowsAsync<InvalidRefreshTokenException>(() => _service.RefreshAsync(refreshed.RefreshToken!));
        }
    }
}
