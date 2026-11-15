using System.Globalization;
using System.Security.Cryptography;
using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Settings;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Infrastructure.Services.Payments
{
    public class DailyLimitPolicy
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IBusinessCalendar _businessCalendar;
        private readonly SecuritySettings _settings;

        public DailyLimitPolicy(
            ITransactionRepository transactionRepository,
            IBusinessCalendar businessCalendar,
            IOptions<SecuritySettings> settings)
        {
            _transactionRepository = transactionRepository;
            _businessCalendar = businessCalendar;
            _settings = settings.Value;
        }

        public async Task<decimal> GetRemainingTodayAsync(Account account)
        {
            var (sentToday, _) = await _transactionRepository
                .GetOutgoingSinceAsync(account.Id, _businessCalendar.StartOfTodayUtc());
            return Math.Max(0, _settings.GetDailyLimit(account.Currency) - sentToday);
        }

        public async Task EnsureWithinLimitsAsync(Account account, decimal amount)
        {
            var (sentToday, countToday) = await _transactionRepository
                .GetOutgoingSinceAsync(account.Id, _businessCalendar.StartOfTodayUtc());

            if (countToday >= _settings.MaxTransactionsPerDay)
                throw new BusinessRuleException(
                    $"Dostignut je maksimalan broj odlaznih transakcija za danas ({_settings.MaxTransactionsPerDay}).");

            var dailyLimit = _settings.GetDailyLimit(account.Currency);
            if (sentToday + amount > dailyLimit)
            {
                var remaining = Math.Max(0, dailyLimit - sentToday);
                throw new BusinessRuleException(
                    $"Prekoračen je dnevni limit od {MoneyRules.Format(dailyLimit, account.Currency)}. " +
                    $"Preostalo za danas: {MoneyRules.Format(remaining, account.Currency)}.");
            }
        }
    }

    public record LedgerLine(int LedgerAccountId, decimal SignedAmount, decimal? BalanceAfter = null);

    public class LedgerRecorder
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly ILedgerRepository _ledgerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly TimeProvider _timeProvider;

        public LedgerRecorder(
            ITransactionRepository transactionRepository,
            ILedgerRepository ledgerRepository,
            IUnitOfWork unitOfWork,
            TimeProvider timeProvider)
        {
            _transactionRepository = transactionRepository;
            _ledgerRepository = ledgerRepository;
            _unitOfWork = unitOfWork;
            _timeProvider = timeProvider;
        }

        public async Task<JournalEntry> RecordAsync(Transaction transaction, params LedgerLine[] lines)
        {
            await _transactionRepository.AddAsync(transaction);
            await _unitOfWork.SaveChangesAsync();

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var entry = JournalEntry.Create(
                transaction.Type.ToString(),
                transaction.Description ?? $"{transaction.Type} {transaction.ReferenceNumber}",
                transaction.PerformedByUserId,
                now);
            entry.TransactionId = transaction.Id;

            foreach (var line in lines)
            {
                if (line.SignedAmount > 0)
                    entry.Credit(line.LedgerAccountId, line.SignedAmount, transaction.Currency, line.BalanceAfter);
                else
                    entry.Debit(line.LedgerAccountId, -line.SignedAmount, transaction.Currency, line.BalanceAfter);
            }

            entry.EnsureBalanced();
            await _ledgerRepository.AddJournalEntryAsync(entry);
            await _unitOfWork.SaveChangesAsync();
            entry.MarkPosted(now);
            return entry;
        }
    }
}
