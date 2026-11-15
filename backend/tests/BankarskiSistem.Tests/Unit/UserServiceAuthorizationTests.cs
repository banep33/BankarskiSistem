using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Services;
using BankarskiSistem.Tests.Fakes;

namespace BankarskiSistem.Tests.Unit
{
    public class UserServiceAuthorizationTests
    {
        private readonly InMemoryUserRepository _users = new();
        private readonly UserService _service;
        private readonly User _admin;
        private readonly User _banker;
        private readonly User _customer;

        public UserServiceAuthorizationTests()
        {
            _service = new UserService(_users, new FakeUnitOfWork(), new FakePasswordHasher(), new NoSessionCache(), new FakeTimeProvider());
            _admin = AddUser("admin@test.local", UserRole.Administrator);
            _banker = AddUser("bankar@test.local", UserRole.Bankar);
            _customer = AddUser("klijent@test.local", UserRole.Korisnik);
        }

        private User AddUser(string email, UserRole role)
        {
            var user = new User { FirstName = "Test", LastName = role.ToString(), Email = email, Role = role, IsActive = true };
            _users.AddAsync(user).Wait();
            return user;
        }

        private Actor AdminActor => new(_admin.Id, UserRole.Administrator);
        private Actor BankerActor => new(_banker.Id, UserRole.Bankar);
        private Actor CustomerActor => new(_customer.Id, UserRole.Korisnik);

        [Fact]
        public async Task Banker_CannotCreateAdministrator()
        {
            var command = new CreateUserCommand("Zli", "Admin", "zli@test.local", "Lozinka123", UserRole.Administrator);
            await Assert.ThrowsAsync<ForbiddenException>(() => _service.CreateAsync(command, BankerActor));
        }

        [Fact]
        public async Task Banker_CanCreateCustomer_WithNormalizedEmail()
        {
            var command = new CreateUserCommand("Nova", "Klijentkinja", "  Nova@Test.Local ", "Lozinka123", UserRole.Korisnik);

            var created = await _service.CreateAsync(command, BankerActor);

            Assert.Equal("nova@test.local", created.Email);
            Assert.Equal(UserRole.Korisnik, created.Role);
        }

        [Fact]
        public async Task Banker_CannotChangeRoles()
        {
            var command = new UpdateUserCommand("Test", "X", _customer.Email, UserRole.Bankar);
            await Assert.ThrowsAsync<ForbiddenException>(() => _service.UpdateAsync(_customer.Id, command, BankerActor));
        }

        [Fact]
        public async Task Banker_CannotManageStaff()
        {
            await Assert.ThrowsAsync<ForbiddenException>(() => _service.DeactivateAsync(_admin.Id, BankerActor));
            await Assert.ThrowsAsync<ForbiddenException>(() => _service.GetByIdAsync(_banker.Id, BankerActor));
        }

        [Fact]
        public async Task Customer_CannotListUsers()
        {
            await Assert.ThrowsAsync<ForbiddenException>(
                () => _service.GetPagedAsync(new UserFilter(), new PageRequest(), CustomerActor));
        }

        [Fact]
        public async Task Banker_UserListIsScopedToCustomers()
        {
            var result = await _service.GetPagedAsync(new UserFilter { Role = UserRole.Administrator }, new PageRequest(), BankerActor);
            Assert.All(result.Items, u => Assert.Equal(UserRole.Korisnik, u.Role));
        }

        [Fact]
        public async Task Administrator_CannotChangeOwnRole()
        {
            var command = new UpdateUserCommand("Test", "Admin", _admin.Email, UserRole.Korisnik);
            await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateAsync(_admin.Id, command, AdminActor));
        }

        [Fact]
        public async Task RoleChange_InvalidatesTokens()
        {
            var versionBefore = _banker.TokenVersion;
            await _service.UpdateAsync(_banker.Id, new UpdateUserCommand("Test", "B", _banker.Email, UserRole.Korisnik), AdminActor);
            Assert.True(_banker.TokenVersion > versionBefore);
        }

        [Fact]
        public async Task Deactivate_InvalidatesTokens_AndSelfDeactivationIsBlocked()
        {
            var versionBefore = _customer.TokenVersion;
            await _service.DeactivateAsync(_customer.Id, BankerActor);

            Assert.False(_customer.IsActive);
            Assert.True(_customer.TokenVersion > versionBefore);
            await Assert.ThrowsAsync<BusinessRuleException>(() => _service.DeactivateAsync(_admin.Id, AdminActor));
        }

        [Fact]
        public async Task PermanentDelete_IsBlockedWhenUserHasFinancialHistory()
        {
            _users.UsersWithHistory.Add(_customer.Id);
            await Assert.ThrowsAsync<ConflictException>(() => _service.PermanentDeleteAsync(_customer.Id, AdminActor));
        }

        [Fact]
        public async Task DuplicateEmail_IsRejected()
        {
            var command = new CreateUserCommand("Dupli", "Email", "KLIJENT@test.local", "Lozinka123", UserRole.Korisnik);
            await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(command, AdminActor));
        }
    }
}
