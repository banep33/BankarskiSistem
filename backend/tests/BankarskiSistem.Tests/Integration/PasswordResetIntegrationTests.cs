using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Tests.Integration
{
    [Collection(MySqlCollection.Name)]
    public class PasswordResetIntegrationTests
    {
        private const string NewPassword = "NovaLozinka456";
        private readonly MySqlDatabaseFixture _database;

        public PasswordResetIntegrationTests(MySqlDatabaseFixture database)
        {
            _database = database;
        }

        private async Task<User> CreateUserAsync(UserRole role, bool locked = false)
        {
            await using var context = _database.CreateContext();
            var user = new User
            {
                FirstName = "Reset",
                LastName = "Test",
                Email = $"{Guid.NewGuid():N}@test.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Lozinka123", 4),
                Role = role,
                IsLocked = locked,
                FailedLoginAttempts = locked ? 5 : 0,
                LockedUntil = locked ? DateTime.UtcNow.AddMinutes(30) : null,
                KycStatus = role == UserRole.Korisnik ? KycStatus.Verifikovan : KycStatus.NijePrimenljivo
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            return user;
        }

        private IntegrationServices Services() => new(_database.CreateContext());

        [MySqlFact]
        public async Task UnknownEmail_CreatesNothingAndRevealsNothing()
        {
            var result = await Services().PasswordResets.SubmitAsync($"{Guid.NewGuid():N}@nepostoji.local", NewPassword);

            Assert.Null(result);
        }

        [MySqlFact]
        public async Task ApprovedRequest_ReplacesPassword_UnlocksAccount_AndRevokesSessions()
        {
            var customer = await CreateUserAsync(UserRole.Korisnik, locked: true);
            var admin = await CreateUserAsync(UserRole.Administrator);
            var session = await Services().Sessions.StartAsync(customer.Id, DateTime.UtcNow.AddMinutes(30));

            var request = await Services().PasswordResets.SubmitAsync(customer.Email.ToUpperInvariant(), NewPassword);
            Assert.NotNull(request);
            Assert.Equal(PasswordResetStatus.Podnet, request!.Status);

            await using (var check = _database.CreateContext())
            {
                var unchanged = await check.Users.SingleAsync(u => u.Id == customer.Id);
                Assert.True(BCrypt.Net.BCrypt.Verify("Lozinka123", unchanged.PasswordHash));
            }

            var approved = await Services().PasswordResets.ApproveAsync(request.Id, "Identitet potvrđen telefonom.", new Actor(admin.Id, UserRole.Administrator));
            Assert.Equal(PasswordResetStatus.Odobren, approved.Status);

            await using var verify = _database.CreateContext();
            var updated = await verify.Users.SingleAsync(u => u.Id == customer.Id);
            Assert.True(BCrypt.Net.BCrypt.Verify(NewPassword, updated.PasswordHash));
            Assert.False(updated.IsLocked);
            Assert.Equal(0, updated.FailedLoginAttempts);
            Assert.Equal(customer.TokenVersion + 1, updated.TokenVersion);
            Assert.NotNull((await verify.UserSessions.SingleAsync(s => s.Id == session.Id)).RevokedAt);

            var stored = await verify.PasswordResetRequests.SingleAsync(r => r.Id == request.Id);
            Assert.Null(stored.NewPasswordHash);
            Assert.Equal(admin.Id, stored.ReviewedByUserId);
        }

        [MySqlFact]
        public async Task NewRequest_SupersedesPendingOne_AndDailyLimitIsSilent()
        {
            var customer = await CreateUserAsync(UserRole.Korisnik);

            var first = await Services().PasswordResets.SubmitAsync(customer.Email, NewPassword);
            var second = await Services().PasswordResets.SubmitAsync(customer.Email, NewPassword + "x");
            var third = await Services().PasswordResets.SubmitAsync(customer.Email, NewPassword + "y");
            var fourth = await Services().PasswordResets.SubmitAsync(customer.Email, NewPassword + "z");

            Assert.NotNull(second);
            Assert.NotNull(third);
            Assert.Null(fourth);

            await using var verify = _database.CreateContext();
            var stored = await verify.PasswordResetRequests.Where(r => r.UserId == customer.Id).ToListAsync();
            Assert.Equal(3, stored.Count);
            Assert.Single(stored, r => r.Status == PasswordResetStatus.Podnet);
            Assert.Equal(PasswordResetStatus.Istekao, stored.Single(r => r.Id == first!.Id).Status);
            Assert.All(stored.Where(r => r.Status != PasswordResetStatus.Podnet), r => Assert.Null(r.NewPasswordHash));
        }

        [MySqlFact]
        public async Task OnlyAnotherAdministrator_CanDecide_AndRejectionKeepsPassword()
        {
            var admin = await CreateUserAsync(UserRole.Administrator);
            var otherAdmin = await CreateUserAsync(UserRole.Administrator);
            var banker = await CreateUserAsync(UserRole.Bankar);

            var own = await Services().PasswordResets.SubmitAsync(admin.Email, NewPassword);
            Assert.NotNull(own);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Services().PasswordResets.ApproveAsync(own!.Id, null, new Actor(admin.Id, UserRole.Administrator)));
            await Assert.ThrowsAsync<ForbiddenException>(() =>
                Services().PasswordResets.ApproveAsync(own!.Id, null, new Actor(banker.Id, UserRole.Bankar)));
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Services().PasswordResets.RejectAsync(own!.Id, " ", new Actor(otherAdmin.Id, UserRole.Administrator)));

            var rejected = await Services().PasswordResets.RejectAsync(own!.Id, "Identitet nije potvrđen.", new Actor(otherAdmin.Id, UserRole.Administrator));
            Assert.Equal(PasswordResetStatus.Odbijen, rejected.Status);
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Services().PasswordResets.ApproveAsync(own.Id, null, new Actor(otherAdmin.Id, UserRole.Administrator)));

            await using var verify = _database.CreateContext();
            Assert.True(BCrypt.Net.BCrypt.Verify("Lozinka123", (await verify.Users.SingleAsync(u => u.Id == admin.Id)).PasswordHash));
        }

        [MySqlFact]
        public async Task ExpiredRequest_CannotBeApproved()
        {
            var customer = await CreateUserAsync(UserRole.Korisnik);
            var admin = await CreateUserAsync(UserRole.Administrator);
            var request = await Services().PasswordResets.SubmitAsync(customer.Email, NewPassword);
            Assert.NotNull(request);

            await using (var context = _database.CreateContext())
            {
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE password_reset_requests SET ExpiresAt = {DateTime.UtcNow.AddMinutes(-1)} WHERE Id = {request!.Id}");
            }

            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                Services().PasswordResets.ApproveAsync(request!.Id, null, new Actor(admin.Id, UserRole.Administrator)));

            await using var verify = _database.CreateContext();
            var stored = await verify.PasswordResetRequests.SingleAsync(r => r.Id == request!.Id);
            Assert.Equal(PasswordResetStatus.Istekao, stored.Status);
            Assert.Null(stored.NewPasswordHash);
        }
    }
}
