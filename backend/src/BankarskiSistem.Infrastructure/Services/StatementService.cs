using System.Globalization;
using System.Text;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Services
{
    public class StatementService : IStatementService
    {
        public const int MaxPeriodDays = 366;
        private static readonly CultureInfo Serbian = CultureInfo.GetCultureInfo("sr-Latn-RS");

        private readonly ApplicationDbContext _context;
        private readonly IBusinessCalendar _businessCalendar;
        private readonly TimeProvider _timeProvider;

        public StatementService(ApplicationDbContext context, IBusinessCalendar businessCalendar, TimeProvider timeProvider)
        {
            _context = context;
            _businessCalendar = businessCalendar;
            _timeProvider = timeProvider;
        }

        public async Task<AccountStatement> GetAsync(int accountId, DateOnly from, DateOnly to, Actor actor)
        {
            if (to < from)
                throw new BusinessRuleException("Datum „do“ ne može biti pre datuma „od“.");
            if (to.DayNumber - from.DayNumber + 1 > MaxPeriodDays)
                throw new BusinessRuleException($"Izvod može obuhvatiti najviše {MaxPeriodDays} dana.");
            var today = _businessCalendar.Today();
            if (from > today)
                throw new BusinessRuleException("Period izvoda ne može početi u budućnosti.");
            if (to > today)
                to = today;

            var account = await _context.Accounts.AsNoTracking().Include(a => a.User).FirstOrDefaultAsync(a => a.Id == accountId)
                ?? throw new NotFoundException("Račun nije pronađen.");
            AccountAccessPolicy.EnsureCanView(account, actor);

            var ledgerId = await _context.LedgerAccounts.Where(l => l.AccountId == accountId).Select(l => l.Id).SingleAsync();
            var fromUtc = _businessCalendar.ToUtc(from.ToDateTime(TimeOnly.MinValue));
            var toUtc = _businessCalendar.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));

            var opening = await _context.Postings.Where(p => p.LedgerAccountId == ledgerId && p.CreatedAt < fromUtc).SumAsync(p => (decimal?)p.Amount) ?? 0m;
            var movements = await (
                from posting in _context.Postings
                join entry in _context.JournalEntries on posting.JournalEntryId equals entry.Id
                where posting.LedgerAccountId == ledgerId && posting.CreatedAt >= fromUtc && posting.CreatedAt < toUtc
                orderby posting.CreatedAt, posting.Id
                select new { posting.Amount, posting.CreatedAt, entry.TransactionId, entry.Description }).ToListAsync();

            var transactionIds = movements.Where(m => m.TransactionId.HasValue).Select(m => m.TransactionId!.Value).Distinct().ToList();
            var transactions = await _context.Transactions.AsNoTracking()
                .Include(t => t.FromAccount)
                .Include(t => t.ToAccount)
                .Include(t => t.PaymentOrder)
                .Where(t => transactionIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id);
            var reversedIds = transactions.Values.Where(t => t.ReversalOfTransactionId.HasValue).Select(t => t.ReversalOfTransactionId!.Value).ToList();
            var originals = await _context.Transactions.AsNoTracking()
                .Where(t => reversedIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, t => t.ReferenceNumber);

            var balance = opening;
            var lines = new List<StatementLine>(movements.Count);
            foreach (var movement in movements)
            {
                balance += movement.Amount;
                var transaction = movement.TransactionId.HasValue ? transactions.GetValueOrDefault(movement.TransactionId.Value) : null;
                lines.Add(new StatementLine(
                    _businessCalendar.ToBusinessTime(movement.CreatedAt),
                    transaction?.ReferenceNumber ?? "Početno stanje",
                    transaction?.Type,
                    transaction?.Description ?? movement.Description,
                    Counterparty(transaction, accountId, originals),
                    movement.Amount < 0 ? -movement.Amount : 0m,
                    movement.Amount > 0 ? movement.Amount : 0m,
                    balance,
                    transaction?.Id));
            }

            return new AccountStatement(account.Id, account.AccountNumber, account.Currency, account.UserId, account.User?.FullName ?? string.Empty, from, to,
                opening, lines.Sum(l => l.Debit), lines.Sum(l => l.Credit), balance, _timeProvider.GetUtcNow().UtcDateTime, lines);
        }

        public static string ToCsv(AccountStatement statement)
        {
            var builder = new StringBuilder();
            builder.Append('﻿');
            AppendRow(builder, "Izvod po računu", statement.AccountNumber);
            AppendRow(builder, "Vlasnik", statement.OwnerName);
            AppendRow(builder, "Valuta", statement.Currency);
            AppendRow(builder, "Period", $"{statement.From:dd.MM.yyyy} - {statement.To:dd.MM.yyyy}");
            AppendRow(builder, "Početno stanje", Amount(statement.OpeningBalance));
            builder.AppendLine();
            AppendRow(builder, "Datum", "Referenca", "Vrsta", "Opis", "Protivstrana", "Duguje", "Potražuje", "Stanje");
            foreach (var line in statement.Lines)
            {
                AppendRow(builder, line.Date.ToString("dd.MM.yyyy HH:mm", Serbian), line.Reference, line.Type?.ToString() ?? "", line.Description,
                    line.Counterparty, line.Debit == 0 ? "" : Amount(line.Debit), line.Credit == 0 ? "" : Amount(line.Credit), Amount(line.BalanceAfter));
            }
            builder.AppendLine();
            AppendRow(builder, "Ukupno duguje", Amount(statement.TotalDebits));
            AppendRow(builder, "Ukupno potražuje", Amount(statement.TotalCredits));
            AppendRow(builder, "Krajnje stanje", Amount(statement.ClosingBalance));
            return builder.ToString();
        }

        public static string EscapeCsv(string value)
        {
            var safe = value.Length > 0 && "=+-@\t\r".Contains(value[0]) ? "'" + value : value;
            return safe.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? $"\"{safe.Replace("\"", "\"\"")}\"" : safe;
        }

        private static void AppendRow(StringBuilder builder, params string[] cells) =>
            builder.Append(string.Join(';', cells.Select(EscapeCsv))).Append("\r\n");

        private static string Amount(decimal value) => value.ToString("0.00", Serbian);

        private static string Counterparty(Transaction? transaction, int accountId, IReadOnlyDictionary<int, string> originals)
        {
            if (transaction == null)
                return "Banka";
            return transaction.Type switch
            {
                TransactionType.Uplata => "Uplata na šalteru",
                TransactionType.Isplata => "Isplata gotovine",
                TransactionType.Konverzija => "Menjačnica",
                TransactionType.Storno => transaction.ReversalOfTransactionId.HasValue && originals.TryGetValue(transaction.ReversalOfTransactionId.Value, out var reference)
                    ? $"Storno {reference}"
                    : "Storno",
                TransactionType.EksterniTransfer => $"{transaction.PaymentOrder?.RecipientName} {transaction.PaymentOrder?.RecipientAccountNumber}".Trim(),
                _ => transaction.FromAccountId == accountId
                    ? $"{transaction.PaymentOrder?.RecipientName ?? "Primalac"} {transaction.ToAccount?.AccountNumber}".Trim()
                    : $"Sa računa {transaction.FromAccount?.AccountNumber}"
            };
        }
    }
}
