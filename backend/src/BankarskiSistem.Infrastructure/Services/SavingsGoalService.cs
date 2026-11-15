using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Services
{
    public class SavingsGoalService : ISavingsGoalService
    {
        public const int MaxActiveGoals = 10;
        private const int MaxAccountNumberAttempts = 10;

        private readonly ApplicationDbContext _context;
        private readonly IPaymentOrderService _paymentOrders;
        private readonly IAccountNumberGenerator _accountNumberGenerator;
        private readonly ILedgerRepository _ledgerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly IBusinessCalendar _businessCalendar;
        private readonly TimeProvider _timeProvider;

        public SavingsGoalService(
            ApplicationDbContext context,
            IPaymentOrderService paymentOrders,
            IAccountNumberGenerator accountNumberGenerator,
            ILedgerRepository ledgerRepository,
            IUnitOfWork unitOfWork,
            INotificationPublisher notificationPublisher,
            IBusinessCalendar businessCalendar,
            TimeProvider timeProvider)
        {
            _context = context;
            _paymentOrders = paymentOrders;
            _accountNumberGenerator = accountNumberGenerator;
            _ledgerRepository = ledgerRepository;
            _unitOfWork = unitOfWork;
            _notificationPublisher = notificationPublisher;
            _businessCalendar = businessCalendar;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<IReadOnlyList<SavingsGoal>> GetMineAsync(Actor actor)
        {
            EnsureCustomer(actor);
            return await _context.SavingsGoals.AsNoTracking().Include(g => g.Account)
                .Where(g => g.UserId == actor.UserId)
                .OrderBy(g => g.Status == SavingsGoalStatus.Zatvoren)
                .ThenBy(g => g.TargetDate)
                .ThenBy(g => g.CreatedAt)
                .ToListAsync();
        }

        public async Task<SavingsGoal> CreateAsync(Actor actor, string name, decimal targetAmount, DateOnly? targetDate, string currency)
        {
            EnsureCustomer(actor);
            ValidateGoal(name, targetAmount, targetDate);
            var owner = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == actor.UserId);
            if (owner.KycStatus != KycStatus.Verifikovan)
                throw new BusinessRuleException("Cilj štednje se može otvoriti tek kada banka verifikuje vaš identitet.");
            var normalized = currency.Trim().ToUpperInvariant();
            if (!await _context.Accounts.AnyAsync(a => a.UserId == actor.UserId && a.Kind == AccountKind.Tekuci && a.Currency == normalized && a.Status == AccountStatus.Aktivan))
                throw new BusinessRuleException($"Za cilj u valuti {normalized} potreban je aktivan tekući račun u istoj valuti.");
            if (await _context.SavingsGoals.CountAsync(g => g.UserId == actor.UserId && g.Status != SavingsGoalStatus.Zatvoren) >= MaxActiveGoals)
                throw new BusinessRuleException($"Možete imati najviše {MaxActiveGoals} otvorenih ciljeva štednje.");

            var goal = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var account = new Account
                {
                    UserId = actor.UserId,
                    AccountNumber = await GenerateAccountNumberAsync(),
                    Balance = 0m,
                    Currency = normalized,
                    Kind = AccountKind.Stednja,
                    Status = AccountStatus.Aktivan,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                };
                await _ledgerRepository.EnsureSystemAccountsAsync(normalized);
                await _context.Accounts.AddAsync(account);
                await _unitOfWork.SaveChangesAsync();
                await _ledgerRepository.AddLedgerAccountAsync(LedgerAccount.ForCustomer(account, UtcNow));

                var created = new SavingsGoal
                {
                    UserId = actor.UserId,
                    AccountId = account.Id,
                    Name = name.Trim(),
                    TargetAmount = targetAmount,
                    TargetDate = targetDate,
                    Status = SavingsGoalStatus.Aktivan,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                };
                await _context.SavingsGoals.AddAsync(created);
                await _unitOfWork.SaveChangesAsync();
                return created;
            });
            return await LoadAsync(goal.Id, actor);
        }

        public async Task<SavingsGoal> UpdateAsync(Actor actor, int goalId, string name, decimal targetAmount, DateOnly? targetDate)
        {
            ValidateGoal(name, targetAmount, targetDate);
            var goal = await LoadTrackedAsync(goalId, actor);
            EnsureOpen(goal);
            goal.Name = name.Trim();
            goal.TargetAmount = targetAmount;
            goal.TargetDate = targetDate;
            goal.UpdatedAt = UtcNow;
            UpdateAchievement(goal);
            await _context.SaveChangesAsync();
            return await LoadAsync(goalId, actor);
        }

        public async Task<SavingsGoal> ContributeAsync(Actor actor, int goalId, int fromAccountId, decimal amount, string? idempotencyKey)
        {
            var goal = await LoadAsync(goalId, actor);
            EnsureOpen(goal);
            await TransferAsync(actor, fromAccountId, goal.Account!.AccountNumber, amount, $"Uplata na cilj: {goal.Name}", idempotencyKey);
            return await RefreshAchievementAsync(goalId, actor);
        }

        public async Task<SavingsGoal> WithdrawAsync(Actor actor, int goalId, int toAccountId, decimal amount, string? idempotencyKey)
        {
            var goal = await LoadAsync(goalId, actor);
            EnsureOpen(goal);
            var target = await OwnCurrentAccountAsync(actor, toAccountId);
            await TransferAsync(actor, goal.AccountId, target.AccountNumber, amount, $"Povlačenje sa cilja: {goal.Name}", idempotencyKey);
            return await RefreshAchievementAsync(goalId, actor);
        }

        public async Task<SavingsGoal> CloseAsync(Actor actor, int goalId, int toAccountId)
        {
            var goal = await LoadAsync(goalId, actor);
            EnsureOpen(goal);
            var target = await OwnCurrentAccountAsync(actor, toAccountId);
            if (goal.Account!.Balance > 0)
                await TransferAsync(actor, goal.AccountId, target.AccountNumber, goal.Account.Balance, $"Zatvaranje cilja: {goal.Name}", null);

            var tracked = await LoadTrackedAsync(goalId, actor);
            var account = await _context.Accounts.FirstAsync(a => a.Id == tracked.AccountId);
            if (account.Balance != 0)
                throw new ConflictException("Stanje štednog računa se promenilo tokom zatvaranja. Pokušajte ponovo.");
            account.Status = AccountStatus.Neaktivan;
            account.UpdatedAt = UtcNow;
            tracked.Status = SavingsGoalStatus.Zatvoren;
            tracked.ClosedAt = UtcNow;
            tracked.UpdatedAt = UtcNow;
            await _context.SaveChangesAsync();
            return await LoadAsync(goalId, actor);
        }

        private async Task TransferAsync(Actor actor, int fromAccountId, string toAccountNumber, decimal amount, string purpose, string? idempotencyKey)
        {
            var source = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == fromAccountId && a.UserId == actor.UserId)
                ?? throw new NotFoundException("Račun nije pronađen.");
            await _paymentOrders.CreateAsync(new CreatePaymentOrderCommand(
                source.Id, "Sopstveni račun", toAccountNumber, null, amount, "289", purpose.Length > 140 ? purpose[..140] : purpose,
                null, null, null, null, idempotencyKey, StrongConfirmationProvided: true), actor);
        }

        private async Task<SavingsGoal> RefreshAchievementAsync(int goalId, Actor actor)
        {
            var goal = await LoadTrackedAsync(goalId, actor);
            var wasAchieved = goal.Status == SavingsGoalStatus.Ostvaren;
            UpdateAchievement(goal);
            if (!wasAchieved && goal.Status == SavingsGoalStatus.Ostvaren)
            {
                await _notificationPublisher.PublishAsync(new Notification
                {
                    UserId = goal.UserId,
                    Type = NotificationType.Stednja,
                    Title = $"Cilj je ostvaren: {goal.Name}",
                    Message = $"Uštedeli ste {MoneyRules.Format(goal.Account!.Balance, goal.Account.Currency)} od planiranih {MoneyRules.Format(goal.TargetAmount, goal.Account.Currency)}. Čestitamo!",
                    CreatedAt = UtcNow
                });
            }
            goal.UpdatedAt = UtcNow;
            await _context.SaveChangesAsync();
            return await LoadAsync(goalId, actor);
        }

        private void UpdateAchievement(SavingsGoal goal)
        {
            if (goal.Status == SavingsGoalStatus.Zatvoren || goal.Account == null)
                return;
            if (goal.Account.Balance >= goal.TargetAmount)
            {
                if (goal.Status != SavingsGoalStatus.Ostvaren)
                    goal.AchievedAt = UtcNow;
                goal.Status = SavingsGoalStatus.Ostvaren;
            }
            else
            {
                goal.Status = SavingsGoalStatus.Aktivan;
                goal.AchievedAt = null;
            }
        }

        private void ValidateGoal(string name, decimal targetAmount, DateOnly? targetDate)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 60)
                throw new BusinessRuleException("Naziv cilja je obavezan i može imati najviše 60 karaktera.");
            MoneyRules.EnsureValidAmount(targetAmount);
            if (targetDate.HasValue && targetDate.Value <= _businessCalendar.Today())
                throw new BusinessRuleException("Datum cilja mora biti u budućnosti.");
        }

        private static void EnsureOpen(SavingsGoal goal)
        {
            if (goal.Status == SavingsGoalStatus.Zatvoren)
                throw new BusinessRuleException("Cilj je zatvoren.");
        }

        private async Task<Account> OwnCurrentAccountAsync(Actor actor, int accountId) =>
            await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.Id == accountId && a.UserId == actor.UserId && a.Kind == AccountKind.Tekuci)
            ?? throw new NotFoundException("Tekući račun nije pronađen.");

        private async Task<SavingsGoal> LoadAsync(int goalId, Actor actor)
        {
            EnsureCustomer(actor);
            return await _context.SavingsGoals.AsNoTracking().Include(g => g.Account).FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == actor.UserId)
                ?? throw new NotFoundException("Cilj štednje nije pronađen.");
        }

        private async Task<SavingsGoal> LoadTrackedAsync(int goalId, Actor actor)
        {
            EnsureCustomer(actor);
            foreach (var entry in _context.ChangeTracker.Entries<Account>().ToList())
                entry.State = EntityState.Detached;
            return await _context.SavingsGoals.Include(g => g.Account).FirstOrDefaultAsync(g => g.Id == goalId && g.UserId == actor.UserId)
                ?? throw new NotFoundException("Cilj štednje nije pronađen.");
        }

        private async Task<string> GenerateAccountNumberAsync()
        {
            for (var attempt = 0; attempt < MaxAccountNumberAttempts; attempt++)
            {
                var candidate = _accountNumberGenerator.Generate();
                if (!await _context.Accounts.AnyAsync(a => a.AccountNumber == candidate))
                    return candidate;
            }
            throw new InvalidOperationException("Nije moguće generisati jedinstven broj računa.");
        }

        private static void EnsureCustomer(Actor actor)
        {
            if (!actor.IsCustomer)
                throw new ForbiddenException("Ciljeve štednje koristi samo klijent.");
        }
    }
}
