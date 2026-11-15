using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Core.Settings;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Infrastructure.Services
{
    public class AccountService : IAccountService
    {
        private const int MaxAccountNumberAttempts = 10;

        private readonly IAccountRepository _accountRepository;
        private readonly IUserRepository _userRepository;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly ILedgerRepository _ledgerRepository;
        private readonly IAccountNumberGenerator _accountNumberGenerator;
        private readonly IUnitOfWork _unitOfWork;
        private readonly BankingSettings _bankingSettings;
        private readonly TimeProvider _timeProvider;

        public AccountService(
            IAccountRepository accountRepository,
            IUserRepository userRepository,
            INotificationPublisher notificationPublisher,
            ILedgerRepository ledgerRepository,
            IAccountNumberGenerator accountNumberGenerator,
            IUnitOfWork unitOfWork,
            IOptions<BankingSettings> bankingSettings,
            TimeProvider timeProvider)
        {
            _accountRepository = accountRepository;
            _userRepository = userRepository;
            _notificationPublisher = notificationPublisher;
            _ledgerRepository = ledgerRepository;
            _accountNumberGenerator = accountNumberGenerator;
            _unitOfWork = unitOfWork;
            _bankingSettings = bankingSettings.Value;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public Task<PagedResult<Account>> GetPagedAsync(string? search, PageRequest page) =>
            _accountRepository.GetPagedAsync(search, page);

        public Task<IReadOnlyList<Account>> GetByUserIdAsync(int userId, Actor actor)
        {
            if (actor.IsCustomer && actor.UserId != userId)
                throw new ForbiddenException();

            return _accountRepository.GetByUserIdAsync(userId);
        }

        public async Task<Account> GetByIdAsync(int id, Actor actor)
        {
            var account = await FindAccountAsync(id);
            AccountAccessPolicy.EnsureCanView(account, actor);
            return account;
        }

        public async Task<Account> CreateAsync(int userId, string currency, Actor actor)
        {
            if (!actor.IsStaff)
                throw new ForbiddenException();

            var normalizedCurrency = currency.Trim().ToUpperInvariant();
            if (!_bankingSettings.SupportedCurrencies.Contains(normalizedCurrency))
                throw new BusinessRuleException(
                    $"Valuta {normalizedCurrency} nije podržana. Dozvoljeno: {string.Join(", ", _bankingSettings.SupportedCurrencies)}.");

            var owner = await _userRepository.GetByIdAsync(userId)
                ?? throw new NotFoundException($"Korisnik sa ID {userId} nije pronađen.");

            if (owner.Role != UserRole.Korisnik)
                throw new BusinessRuleException("Račun se može otvoriti samo klijentu banke.");

            if (!owner.IsActive)
                throw new BusinessRuleException("Ne možete otvoriti račun deaktiviranom korisniku.");

            if (owner.KycStatus != KycStatus.Verifikovan)
                throw new BusinessRuleException("Račun se može otvoriti tek kada se identitet klijenta verifikuje (KYC).");

            var account = new Account
            {
                UserId = owner.Id,
                AccountNumber = await GenerateUniqueAccountNumberAsync(),
                Balance = 0m,
                Currency = normalizedCurrency,
                Status = AccountStatus.Aktivan,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            };

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                await _ledgerRepository.EnsureSystemAccountsAsync(normalizedCurrency);
                await _accountRepository.AddAsync(account);
                await _ledgerRepository.AddLedgerAccountAsync(LedgerAccount.ForCustomer(account, UtcNow));
                await _unitOfWork.SaveChangesAsync();
                await _notificationPublisher.PublishAsync(NotificationFactory.AccountOpened(account, UtcNow));
                return account;
            });

            account.User = owner;
            return account;
        }

        public async Task<Account> ChangeStatusAsync(int accountId, AccountStatus status, Actor actor)
        {
            if (!actor.IsStaff)
                throw new ForbiddenException();

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var locked = await _accountRepository.LockForUpdateAsync([accountId]);
                var account = locked.SingleOrDefault()
                    ?? throw new NotFoundException($"Račun sa ID {accountId} nije pronađen.");

                if (account.Status == status)
                    throw new BusinessRuleException($"Račun je već u statusu {status}.");

                account.Status = status;
                account.UpdatedAt = UtcNow;

                await _notificationPublisher.PublishAsync(NotificationFactory.AccountStatusChanged(account, UtcNow));
                return account;
            });

            return await FindAccountAsync(accountId);
        }

        public async Task DeleteAsync(int accountId, Actor actor)
        {
            if (!actor.IsAdministrator)
                throw new ForbiddenException();

            var account = await FindAccountAsync(accountId);

            if (account.IsActive)
                throw new BusinessRuleException("Aktivan račun se ne može obrisati. Prvo ga deaktivirajte.");

            if (account.Balance != 0)
                throw new BusinessRuleException("Račun sa stanjem različitim od nule se ne može obrisati.");

            if (await _accountRepository.HasTransactionsAsync(account.Id))
                throw new ConflictException(
                    "Račun ima istoriju transakcija i ne može se obrisati. Ostavite ga deaktiviranog radi revizije.");

            _accountRepository.Remove(account);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<Account> FindAccountAsync(int id) =>
            await _accountRepository.GetByIdAsync(id)
            ?? throw new NotFoundException($"Račun sa ID {id} nije pronađen.");

        private async Task<string> GenerateUniqueAccountNumberAsync()
        {
            for (var attempt = 0; attempt < MaxAccountNumberAttempts; attempt++)
            {
                var candidate = _accountNumberGenerator.Generate();
                if (!await _accountRepository.AccountNumberExistsAsync(candidate))
                    return candidate;
            }
            throw new InvalidOperationException("Nije moguće generisati jedinstven broj računa.");
        }
    }

    internal static class AccountAccessPolicy
    {
        public static void EnsureCanView(Account account, Actor actor)
        {
            if (actor.IsStaff || account.UserId == actor.UserId)
                return;
            throw new ForbiddenException("Nemate pristup ovom računu.");
        }

        public static void EnsureOwner(Account account, Actor actor)
        {
            if (account.UserId != actor.UserId)
                throw new ForbiddenException("Nemate pristup ovom računu.");
        }
    }
}
