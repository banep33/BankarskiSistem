using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Security;
using BankarskiSistem.Tests.Fakes;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Tests.Integration
{
    [Collection(MySqlCollection.Name)]
    public class TwoFactorIntegrationTests
    {
        private readonly MySqlDatabaseFixture _database;

        public TwoFactorIntegrationTests(MySqlDatabaseFixture database)
        {
            _database = database;
        }

        private async Task<User> CreateUserAsync(UserRole role = UserRole.Korisnik)
        {
            await using var context = _database.CreateContext();
            var user = new User
            {
                FirstName = "Dva",
                LastName = "Faktora",
                Email = $"{Guid.NewGuid():N}@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lozinka123", 4),
                Role = role,
                KycStatus = role == UserRole.Korisnik ? KycStatus.Verifikovan : KycStatus.NijePrimenljivo
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private IntegrationServices Services() => new(_database.CreateContext());

        private static string CurrentCode(string secret, int stepOffset = 0) =>
            Totp.ComputeCode(Totp.FromBase32(secret), Totp.StepAt(DateTimeOffset.UtcNow) + stepOffset);

        [MySqlFact]
        public async Task Enable_StoresProtectedSecret_AndIssuesSingleUseRecoveryCodes()
        {
            var user = await CreateUserAsync();
            var setup = await Services().TwoFactor.BeginSetupAsync(user.Id);

            await Assert.ThrowsAsync<BusinessRuleException>(() => Services().TwoFactor.EnableAsync(user.Id, "000000"));
            var codes = await Services().TwoFactor.EnableAsync(user.Id, CurrentCode(setup.Secret));

            Assert.Equal(10, codes.Count);
            Assert.Equal(10, codes.Distinct().Count());

            await using (var context = _database.CreateContext())
            {
                var stored = await context.Users.SingleAsync(u => u.Id == user.Id);
                Assert.NotNull(stored.TotpEnabledAt);
                Assert.NotEqual(setup.Secret, stored.TotpSecretProtected);
                Assert.StartsWith("zasticeno:", stored.TotpSecretProtected);
                Assert.DoesNotContain(await context.RecoveryCodes.Where(c => c.UserId == user.Id).Select(c => c.CodeHash).ToListAsync(), h => codes.Contains(h));
            }

            await using (var context = _database.CreateContext())
            {
                var reloaded = await context.Users.SingleAsync(u => u.Id == user.Id);
                Assert.True(await new IntegrationServices(context).TwoFactor.VerifyLoginCodeAsync(reloaded, codes[0].ToLowerInvariant()));
            }
            await using (var context = _database.CreateContext())
            {
                var reloaded = await context.Users.SingleAsync(u => u.Id == user.Id);
                Assert.False(await new IntegrationServices(context).TwoFactor.VerifyLoginCodeAsync(reloaded, codes[0]));
            }

            var status = await Services().TwoFactor.GetStatusAsync(user.Id);
            Assert.True(status.Enabled);
            Assert.Equal(9, status.RecoveryCodesRemaining);
        }

        [MySqlFact]
        public async Task SameTotpCode_CannotBeUsedTwice()
        {
            var user = await CreateUserAsync();
            var setup = await Services().TwoFactor.BeginSetupAsync(user.Id);
            await Services().TwoFactor.EnableAsync(user.Id, CurrentCode(setup.Secret, -1));

            var code = CurrentCode(setup.Secret);
            await using (var context = _database.CreateContext())
            {
                var reloaded = await context.Users.SingleAsync(u => u.Id == user.Id);
                Assert.True(await new IntegrationServices(context).TwoFactor.VerifyLoginCodeAsync(reloaded, code));
            }
            await using (var context = _database.CreateContext())
            {
                var reloaded = await context.Users.SingleAsync(u => u.Id == user.Id);
                Assert.False(await new IntegrationServices(context).TwoFactor.VerifyLoginCodeAsync(reloaded, code));
            }
        }

        [MySqlFact]
        public async Task AdministratorReset_ClearsTwoFactor_AndRevokesSessions()
        {
            var user = await CreateUserAsync();
            var admin = await CreateUserAsync(UserRole.Administrator);
            var banker = await CreateUserAsync(UserRole.Bankar);
            var setup = await Services().TwoFactor.BeginSetupAsync(user.Id);
            await Services().TwoFactor.EnableAsync(user.Id, CurrentCode(setup.Secret));
            var session = await Services().Sessions.StartAsync(user.Id, DateTime.UtcNow.AddHours(1), OpaqueTokens.Hash("x" + Guid.NewGuid()));

            await Assert.ThrowsAsync<ForbiddenException>(() => Services().TwoFactor.ResetByAdministratorAsync(user.Id, new Actor(banker.Id, UserRole.Bankar)));
            await Services().TwoFactor.ResetByAdministratorAsync(user.Id, new Actor(admin.Id, UserRole.Administrator));

            await using var verify = _database.CreateContext();
            var stored = await verify.Users.SingleAsync(u => u.Id == user.Id);
            Assert.Null(stored.TotpEnabledAt);
            Assert.Null(stored.TotpSecretProtected);
            Assert.Equal(0, await verify.RecoveryCodes.CountAsync(c => c.UserId == user.Id));
            Assert.NotNull((await verify.UserSessions.SingleAsync(s => s.Id == session.Id)).RevokedAt);
        }

        [MySqlFact]
        public async Task RefreshRotation_DetectsReuse_AndRevokesSession()
        {
            var user = await CreateUserAsync();
            var first = OpaqueTokens.Generate();
            var session = await Services().Sessions.StartAsync(user.Id, DateTime.UtcNow.AddHours(1), OpaqueTokens.Hash(first));

            var second = OpaqueTokens.Generate();
            var rotated = await Services().Sessions.RotateRefreshTokenAsync(OpaqueTokens.Hash(first), OpaqueTokens.Hash(second));
            Assert.Equal(RefreshRotationOutcome.Rotated, rotated.Outcome);

            var concurrent = await Services().Sessions.RotateRefreshTokenAsync(OpaqueTokens.Hash(first), OpaqueTokens.Hash(OpaqueTokens.Generate()));
            Assert.Equal(RefreshRotationOutcome.Grace, concurrent.Outcome);

            await using (var context = _database.CreateContext())
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE user_sessions SET RefreshRotatedAt = {DateTime.UtcNow.AddMinutes(-5)} WHERE Id = {session.Id}");
            }

            var reused = await Services().Sessions.RotateRefreshTokenAsync(OpaqueTokens.Hash(first), OpaqueTokens.Hash(OpaqueTokens.Generate()));
            Assert.Equal(RefreshRotationOutcome.Reused, reused.Outcome);

            var afterTheft = await Services().Sessions.RotateRefreshTokenAsync(OpaqueTokens.Hash(second), OpaqueTokens.Hash(OpaqueTokens.Generate()));
            Assert.Equal(RefreshRotationOutcome.Invalid, afterTheft.Outcome);

            await using var verify = _database.CreateContext();
            Assert.NotNull((await verify.UserSessions.SingleAsync(s => s.Id == session.Id)).RevokedAt);
            Assert.True(await verify.Notifications.AnyAsync(n => n.UserId == user.Id && n.Title.Contains("bezbednosnih")));
        }
    }
}
