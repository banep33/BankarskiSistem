using System.Globalization;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BankarskiSistem.Infrastructure.Services
{
    public class BudgetService : IBudgetService
    {
        public const string BudgetCurrency = "RSD";
        private static readonly int[] AlertThresholds = [80, 100];

        private readonly ApplicationDbContext _context;
        private readonly IBusinessCalendar _businessCalendar;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<BudgetService> _logger;

        public BudgetService(
            ApplicationDbContext context,
            IBusinessCalendar businessCalendar,
            INotificationPublisher notificationPublisher,
            TimeProvider timeProvider,
            ILogger<BudgetService> logger)
        {
            _context = context;
            _businessCalendar = businessCalendar;
            _notificationPublisher = notificationPublisher;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<IReadOnlyList<SpendingCategory>> GetCategoriesAsync() =>
            await _context.SpendingCategories.AsNoTracking().OrderBy(c => c.SortOrder).ToListAsync();

        public async Task<SpendingSummary> GetSpendingAsync(Actor actor, string? month)
        {
            EnsureCustomer(actor);
            var period = ParseMonth(month);
            return await BuildAsync(actor.UserId, period);
        }

        public async Task AssignCategoryAsync(Actor actor, int transactionId, int categoryId, bool rememberForCounterparty)
        {
            EnsureCustomer(actor);
            if (!await _context.SpendingCategories.AnyAsync(c => c.Id == categoryId))
                throw new NotFoundException("Kategorija ne postoji.");

            var ownIds = await _context.Accounts.Where(a => a.UserId == actor.UserId).Select(a => a.Id).ToListAsync();
            var transaction = await _context.Transactions.AsNoTracking()
                .Include(t => t.ToAccount)
                .Include(t => t.PaymentOrder)
                .FirstOrDefaultAsync(t => t.Id == transactionId
                    && ((t.FromAccountId != null && ownIds.Contains(t.FromAccountId.Value)) || (t.ToAccountId != null && ownIds.Contains(t.ToAccountId.Value))))
                ?? throw new NotFoundException("Transakcija nije pronađena.");

            var existing = await _context.TransactionCategories.FirstOrDefaultAsync(c => c.UserId == actor.UserId && c.TransactionId == transactionId);
            if (existing == null)
                await _context.TransactionCategories.AddAsync(new TransactionCategory { UserId = actor.UserId, TransactionId = transactionId, CategoryId = categoryId, AssignedAt = UtcNow });
            else
            {
                existing.CategoryId = categoryId;
                existing.AssignedAt = UtcNow;
            }

            var counterparty = CounterpartyAccount(transaction);
            if (rememberForCounterparty)
            {
                if (counterparty == null)
                    throw new BusinessRuleException("Za ovu transakciju nema računa primaoca, pa pravilo ne može da se zapamti.");
                var rule = await _context.CategoryRules.FirstOrDefaultAsync(r => r.UserId == actor.UserId && r.CounterpartyAccountNumber == counterparty);
                if (rule == null)
                    await _context.CategoryRules.AddAsync(new CategoryRule { UserId = actor.UserId, CounterpartyAccountNumber = counterparty, CategoryId = categoryId, CreatedAt = UtcNow });
                else
                    rule.CategoryId = categoryId;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<IReadOnlyList<Budget>> GetBudgetsAsync(Actor actor)
        {
            EnsureCustomer(actor);
            return await _context.Budgets.AsNoTracking().Include(b => b.Category).Where(b => b.UserId == actor.UserId)
                .OrderBy(b => b.Category!.SortOrder).ToListAsync();
        }

        public async Task<Budget> SetBudgetAsync(Actor actor, int categoryId, decimal monthlyLimit)
        {
            EnsureCustomer(actor);
            MoneyRules.EnsureValidAmount(monthlyLimit);
            if (!await _context.SpendingCategories.AnyAsync(c => c.Id == categoryId))
                throw new NotFoundException("Kategorija ne postoji.");

            var budget = await _context.Budgets.FirstOrDefaultAsync(b => b.UserId == actor.UserId && b.CategoryId == categoryId);
            if (budget == null)
            {
                budget = new Budget { UserId = actor.UserId, CategoryId = categoryId, MonthlyLimit = monthlyLimit, CreatedAt = UtcNow, UpdatedAt = UtcNow };
                await _context.Budgets.AddAsync(budget);
            }
            else
            {
                budget.MonthlyLimit = monthlyLimit;
                budget.UpdatedAt = UtcNow;
                await _context.BudgetAlerts.Where(a => a.BudgetId == budget.Id && a.Month == CurrentMonth()).ExecuteDeleteAsync();
            }
            await _context.SaveChangesAsync();
            await EvaluateAlertsAsync(actor.UserId);
            return await _context.Budgets.AsNoTracking().Include(b => b.Category).FirstAsync(b => b.Id == budget.Id);
        }

        public async Task DeleteBudgetAsync(Actor actor, int budgetId)
        {
            EnsureCustomer(actor);
            var deleted = await _context.Budgets.Where(b => b.Id == budgetId && b.UserId == actor.UserId).ExecuteDeleteAsync();
            if (deleted == 0)
                throw new NotFoundException("Budžet nije pronađen.");
        }

        public async Task EvaluateAlertsAsync(int userId)
        {
            var month = CurrentMonth();
            var budgets = await _context.Budgets.Include(b => b.Category).Where(b => b.UserId == userId).ToListAsync();
            if (budgets.Count == 0)
                return;

            var summary = await BuildAsync(userId, ParseMonth(month));
            var sent = await _context.BudgetAlerts.Where(a => budgets.Select(b => b.Id).Contains(a.BudgetId) && a.Month == month)
                .Select(a => new { a.BudgetId, a.Threshold }).ToListAsync();

            foreach (var budget in budgets)
            {
                var spent = summary.Categories.FirstOrDefault(c => c.CategoryId == budget.CategoryId)?.Amount ?? 0m;
                var percent = spent / budget.MonthlyLimit * 100m;
                var reached = AlertThresholds.Where(t => percent >= t && !sent.Any(s => s.BudgetId == budget.Id && s.Threshold == t)).DefaultIfEmpty(0).Max();
                if (reached == 0)
                    continue;

                foreach (var threshold in AlertThresholds.Where(t => t <= reached && !sent.Any(s => s.BudgetId == budget.Id && s.Threshold == t)))
                    await _context.BudgetAlerts.AddAsync(new BudgetAlert { BudgetId = budget.Id, Month = month, Threshold = threshold, CreatedAt = UtcNow });

                await _notificationPublisher.PublishAsync(new Notification
                {
                    UserId = userId,
                    Type = NotificationType.Budzet,
                    Title = reached >= 100 ? $"Prekoračen budžet: {budget.Category!.Name}" : $"Budžet je skoro potrošen: {budget.Category!.Name}",
                    Message = $"Ovog meseca je za kategoriju {budget.Category!.Name} potrošeno {MoneyRules.Format(spent, BudgetCurrency)} " +
                              $"od {MoneyRules.Format(budget.MonthlyLimit, BudgetCurrency)} ({percent:0}%).",
                    CreatedAt = UtcNow
                });
            }
            await _context.SaveChangesAsync();
        }

        public async Task TryEvaluateAlertsAsync(int userId)
        {
            try
            {
                await EvaluateAlertsAsync(userId);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Provera budžeta za korisnika {UserId} nije uspela", userId);
            }
        }

        private async Task<SpendingSummary> BuildAsync(int userId, DateOnly monthStart)
        {
            var accounts = await _context.Accounts.AsNoTracking().Where(a => a.UserId == userId).Select(a => new { a.Id, a.Currency }).ToListAsync();
            var allOwn = accounts.Select(a => a.Id).ToList();
            var rsdOwn = accounts.Where(a => a.Currency == BudgetCurrency).Select(a => a.Id).ToList();
            var fromUtc = _businessCalendar.ToUtc(monthStart.ToDateTime(TimeOnly.MinValue));
            var toUtc = _businessCalendar.ToUtc(monthStart.AddMonths(1).ToDateTime(TimeOnly.MinValue));

            var transactions = await _context.Transactions.AsNoTracking()
                .Include(t => t.ToAccount)
                .Include(t => t.FromAccount)
                .Include(t => t.PaymentOrder)
                .Where(t => t.CreatedAt >= fromUtc && t.CreatedAt < toUtc && t.Status == TransactionStatus.Uspesna && t.ReversedAt == null)
                .Where(t => (t.FromAccountId != null && rsdOwn.Contains(t.FromAccountId.Value)) || (t.ToAccountId != null && rsdOwn.Contains(t.ToAccountId.Value)))
                .ToListAsync();

            var expenses = transactions.Where(t =>
                t.FromAccountId != null && rsdOwn.Contains(t.FromAccountId.Value)
                && (t.Type is TransactionType.Isplata or TransactionType.EksterniTransfer or TransactionType.Kartica
                    || (t.Type == TransactionType.Transfer && !(t.ToAccountId.HasValue && allOwn.Contains(t.ToAccountId.Value))))).ToList();
            var income = transactions.Where(t =>
                t.ToAccountId != null && rsdOwn.Contains(t.ToAccountId.Value)
                && (t.Type == TransactionType.Uplata || (t.Type == TransactionType.Transfer && !(t.FromAccountId.HasValue && allOwn.Contains(t.FromAccountId.Value))))).ToList();

            var expenseIds = expenses.Select(t => t.Id).ToList();
            var cardDetails = await _context.CardAuthorizations.AsNoTracking()
                .Where(a => a.TransactionId != null && expenseIds.Contains(a.TransactionId.Value))
                .ToDictionaryAsync(a => a.TransactionId!.Value);
            var explicitCategories = await _context.TransactionCategories.AsNoTracking()
                .Where(c => c.UserId == userId && expenseIds.Contains(c.TransactionId))
                .ToDictionaryAsync(c => c.TransactionId, c => c.CategoryId);
            var rules = await _context.CategoryRules.AsNoTracking().Where(r => r.UserId == userId)
                .ToDictionaryAsync(r => r.CounterpartyAccountNumber, r => r.CategoryId);
            var categories = await GetCategoriesAsync();
            var byCode = categories.ToDictionary(c => c.Code, c => c.Id);
            var budgets = await _context.Budgets.AsNoTracking().Where(b => b.UserId == userId).ToDictionaryAsync(b => b.CategoryId);

            var categorized = expenses
                .OrderByDescending(t => t.CreatedAt)
                .Select(t =>
                {
                    var counterparty = CounterpartyAccount(t);
                    var isExplicit = explicitCategories.TryGetValue(t.Id, out var categoryId);
                    if (!isExplicit)
                        categoryId = counterparty != null && rules.TryGetValue(counterparty, out var ruled)
                            ? ruled
                            : byCode[CardCategoryCode(cardDetails.GetValueOrDefault(t.Id), byCode) ?? DefaultCategoryCode(t)];
                    return new CategorizedTransaction(t.Id, _businessCalendar.ToBusinessTime(t.CreatedAt), t.ReferenceNumber,
                        t.PaymentOrder?.Purpose ?? t.Description ?? string.Empty,
                        t.Type == TransactionType.Isplata ? "Isplata gotovine" : cardDetails.GetValueOrDefault(t.Id)?.MerchantName ?? t.PaymentOrder?.RecipientName ?? counterparty ?? "Plaćanje",
                        counterparty, t.Amount + t.FeeAmount, categoryId, isExplicit);
                })
                .ToList();

            var spending = categories
                .Select(c =>
                {
                    var items = categorized.Where(t => t.CategoryId == c.Id).ToList();
                    budgets.TryGetValue(c.Id, out var budget);
                    return new CategorySpending(c.Id, c.Code, c.Name, c.Color, items.Sum(t => t.Amount), items.Count, budget?.MonthlyLimit, budget?.Id);
                })
                .Where(c => c.Count > 0 || c.BudgetId != null)
                .OrderByDescending(c => c.Amount)
                .ToList();

            return new SpendingSummary(monthStart.ToString("yyyy-MM", CultureInfo.InvariantCulture), BudgetCurrency,
                income.Sum(t => t.Amount), categorized.Sum(t => t.Amount), spending, categorized);
        }

        private static string? CardCategoryCode(CardAuthorization? authorization, IReadOnlyDictionary<string, int> byCode)
        {
            if (authorization == null)
                return null;
            if (authorization.Channel == CardChannel.Bankomat)
                return "GOTOVINA";
            return authorization.CategoryCode != null && byCode.ContainsKey(authorization.CategoryCode) ? authorization.CategoryCode : "KUPOVINA";
        }

        private static string DefaultCategoryCode(Transaction transaction) => transaction.Type switch
        {
            TransactionType.Isplata => "GOTOVINA",
            _ => transaction.PaymentOrder?.PaymentCode switch
            {
                "221" => "KUPOVINA",
                "253" or "254" => "POREZI",
                _ => "OSTALO"
            }
        };

        private static string? CounterpartyAccount(Transaction transaction) => transaction.Type switch
        {
            TransactionType.EksterniTransfer => transaction.PaymentOrder?.RecipientAccountNumber,
            TransactionType.Transfer => transaction.ToAccount?.AccountNumber ?? transaction.PaymentOrder?.RecipientAccountNumber,
            _ => null
        };

        private DateOnly ParseMonth(string? month)
        {
            if (string.IsNullOrWhiteSpace(month))
                return CurrentMonthStart();
            if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
                throw new BusinessRuleException("Mesec mora biti u formatu GGGG-MM.");
            if (start > CurrentMonthStart())
                throw new BusinessRuleException("Mesec ne može biti u budućnosti.");
            return start;
        }

        private DateOnly CurrentMonthStart()
        {
            var today = _businessCalendar.Today();
            return new DateOnly(today.Year, today.Month, 1);
        }

        private string CurrentMonth() => CurrentMonthStart().ToString("yyyy-MM", CultureInfo.InvariantCulture);

        private static void EnsureCustomer(Actor actor)
        {
            if (!actor.IsCustomer)
                throw new ForbiddenException("Budžete koristi samo klijent.");
        }
    }
}
