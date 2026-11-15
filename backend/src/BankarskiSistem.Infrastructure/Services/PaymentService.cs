using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Services.Payments;

namespace BankarskiSistem.Infrastructure.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly IAccountRepository _accountRepository;
        private readonly ITransactionRepository _transactionRepository;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly IComplianceMonitor _complianceMonitor;
        private readonly ILedgerRepository _ledgerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly DailyLimitPolicy _dailyLimitPolicy;
        private readonly LedgerRecorder _ledgerRecorder;
        private readonly TimeProvider _timeProvider;

        public PaymentService(
            IAccountRepository accountRepository,
            ITransactionRepository transactionRepository,
            INotificationPublisher notificationPublisher,
            IComplianceMonitor complianceMonitor,
            ILedgerRepository ledgerRepository,
            IUnitOfWork unitOfWork,
            DailyLimitPolicy dailyLimitPolicy,
            LedgerRecorder ledgerRecorder,
            TimeProvider timeProvider)
        {
            _accountRepository = accountRepository;
            _transactionRepository = transactionRepository;
            _notificationPublisher = notificationPublisher;
            _complianceMonitor = complianceMonitor;
            _ledgerRepository = ledgerRepository;
            _unitOfWork = unitOfWork;
            _dailyLimitPolicy = dailyLimitPolicy;
            _ledgerRecorder = ledgerRecorder;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<FinancialOperationResult> DepositAsync(DepositCommand command, Actor actor)
        {
            if (!actor.IsStaff)
                throw new ForbiddenException("Uplatu mogu izvršiti samo zaposleni u banci.");

            MoneyRules.EnsureValidAmount(command.Amount);
            var operation = new OperationKey(TransactionType.Uplata, command.AccountId, command.Amount, command.IdempotencyKey);

            return await ExecuteIdempotentAsync(operation, actor, async () =>
            {
                var account = await LockSingleAsync(command.AccountId);
                MoneyRules.EnsureActive(account, "Uplata nije moguća jer račun nije aktivan.");
                _pendingComplianceAccount = account;

                var customerLedgerId = (await _ledgerRepository.GetCustomerLedgerAccountIdsAsync([account.Id]))[account.Id];
                var cashLedgerId = await _ledgerRepository.GetSystemLedgerAccountIdAsync(LedgerAccountType.Blagajna, account.Currency);

                account.Balance += command.Amount;
                account.UpdatedAt = UtcNow;

                var transaction = CreateTransaction(operation, actor, command.Description, account.Currency);
                transaction.ToAccountId = account.Id;
                transaction.ToBalanceAfter = account.Balance;

                await _ledgerRecorder.RecordAsync(transaction,
                    new LedgerLine(customerLedgerId, command.Amount, account.Balance),
                    new LedgerLine(cashLedgerId, -command.Amount));

                await _notificationPublisher.PublishAsync(NotificationFactory.Deposit(account, transaction, UtcNow));
                return new FinancialOperationResult(transaction, account.Balance, null, false);
            });
        }

        public async Task<FinancialOperationResult> WithdrawAsync(WithdrawCommand command, Actor actor)
        {
            if (!actor.IsCustomer)
                throw new ForbiddenException("Isplatu sa računa može izvršiti samo vlasnik računa.");

            MoneyRules.EnsureValidAmount(command.Amount);
            var operation = new OperationKey(TransactionType.Isplata, command.AccountId, command.Amount, command.IdempotencyKey);

            return await ExecuteIdempotentAsync(operation, actor, async () =>
            {
                var account = await LockSingleAsync(command.AccountId);
                AccountAccessPolicy.EnsureOwner(account, actor);
                _pendingComplianceAccount = account;
                MoneyRules.EnsureActive(account, "Isplata nije moguća jer račun nije aktivan.");
                await _dailyLimitPolicy.EnsureWithinLimitsAsync(account, command.Amount);
                MoneyRules.EnsureSufficientFunds(account, command.Amount);

                var customerLedgerId = (await _ledgerRepository.GetCustomerLedgerAccountIdsAsync([account.Id]))[account.Id];
                var cashLedgerId = await _ledgerRepository.GetSystemLedgerAccountIdAsync(LedgerAccountType.Blagajna, account.Currency);

                account.Balance -= command.Amount;
                account.UpdatedAt = UtcNow;

                var transaction = CreateTransaction(operation, actor, command.Description, account.Currency);
                transaction.FromAccountId = account.Id;
                transaction.FromBalanceAfter = account.Balance;

                await _ledgerRecorder.RecordAsync(transaction,
                    new LedgerLine(customerLedgerId, -command.Amount, account.Balance),
                    new LedgerLine(cashLedgerId, command.Amount));

                await _notificationPublisher.PublishAsync(NotificationFactory.Withdrawal(account, transaction, UtcNow));
                return new FinancialOperationResult(transaction, account.Balance, null, false);
            });
        }

        private async Task<FinancialOperationResult> ExecuteIdempotentAsync(
            OperationKey operation, Actor actor, Func<Task<FinancialOperationResult>> execute)
        {
            var replay = await TryReplayAsync(operation, actor);
            if (replay != null)
                return replay;

            try
            {
                var result = await _unitOfWork.ExecuteInTransactionAsync(execute);
                await RunComplianceAsync(result);
                return result;
            }
            catch (DuplicateEntryException) when (operation.IdempotencyKey != null)
            {
                return await TryReplayAsync(operation, actor) ?? throw new ConflictException(
                    "Zahtev sa istim ključem je već u obradi. Pokušajte ponovo.");
            }
        }

        private Account? _pendingComplianceAccount;

        private async Task RunComplianceAsync(FinancialOperationResult result)
        {
            var account = _pendingComplianceAccount;
            _pendingComplianceAccount = null;
            if (account == null)
                return;

            await _complianceMonitor.EvaluateAsync(result.Transaction, account, null);
            await _unitOfWork.SaveChangesAsync();
        }

        private async Task<FinancialOperationResult?> TryReplayAsync(OperationKey operation, Actor actor)
        {
            if (operation.IdempotencyKey == null)
                return null;

            var existing = await _transactionRepository.GetByIdempotencyKeyAsync(actor.UserId, operation.IdempotencyKey);
            if (existing == null)
                return null;

            var accountId = operation.Type == TransactionType.Uplata ? existing.ToAccountId : existing.FromAccountId;
            if (existing.Type != operation.Type || accountId != operation.AccountId || existing.Amount != operation.Amount)
                throw new ConflictException("Ključ idempotentnosti je već iskorišćen za drugačiji zahtev.");

            var balanceAfter = operation.Type == TransactionType.Uplata ? existing.ToBalanceAfter : existing.FromBalanceAfter;
            return new FinancialOperationResult(existing, balanceAfter ?? 0m, null, true);
        }

        private async Task<Account> LockSingleAsync(int accountId)
        {
            var locked = await _accountRepository.LockForUpdateAsync([accountId]);
            return locked.SingleOrDefault() ?? throw new NotFoundException($"Račun sa ID {accountId} nije pronađen.");
        }

        private Transaction CreateTransaction(OperationKey operation, Actor actor, string? description, string currency) => new()
        {
            Amount = operation.Amount,
            Currency = currency,
            Type = operation.Type,
            Status = TransactionStatus.Uspesna,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            ReferenceNumber = MoneyRules.NewReferenceNumber(UtcNow),
            PerformedByUserId = actor.UserId,
            IdempotencyKey = operation.IdempotencyKey,
            CreatedAt = UtcNow
        };

        private sealed record OperationKey(TransactionType Type, int AccountId, decimal Amount, string? IdempotencyKey);
    }
}
