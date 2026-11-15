using System.Security.Cryptography;
using BankarskiSistem.Core.Banking;
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
    public class StandingOrderService : IStandingOrderService
    {
        public const int MaxActivePerUser = 50;
        public const int MaxConsecutiveFailures = 3;
        private const int MaxStartDaysAhead = 365;

        private readonly ApplicationDbContext _context;
        private readonly IPaymentOrderService _paymentOrders;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBusinessCalendar _businessCalendar;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<StandingOrderService> _logger;
        private readonly IBudgetService _budgetService;

        public StandingOrderService(
            ApplicationDbContext context,
            IPaymentOrderService paymentOrders,
            INotificationPublisher notificationPublisher,
            IUnitOfWork unitOfWork,
            IBusinessCalendar businessCalendar,
            TimeProvider timeProvider,
            ILogger<StandingOrderService> logger,
            IBudgetService budgetService)
        {
            _budgetService = budgetService;
            _context = context;
            _paymentOrders = paymentOrders;
            _notificationPublisher = notificationPublisher;
            _unitOfWork = unitOfWork;
            _businessCalendar = businessCalendar;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<IReadOnlyList<StandingOrder>> GetMineAsync(Actor actor)
        {
            EnsureCustomer(actor);
            return await QueryForUser(actor.UserId).ToListAsync();
        }

        public async Task<IReadOnlyList<StandingOrder>> GetForUserAsync(int userId, Actor actor)
        {
            if (!actor.IsStaff)
                throw new ForbiddenException();
            return await QueryForUser(userId).ToListAsync();
        }

        public async Task<StandingOrder> GetAsync(int id, Actor actor)
        {
            EnsureCustomer(actor);
            var order = await _context.StandingOrders.AsNoTracking()
                .Include(o => o.FromAccount)
                .Include(o => o.Executions.OrderByDescending(e => e.ScheduledFor).Take(50))
                    .ThenInclude(e => e.PaymentOrder)
                        .ThenInclude(p => p!.Transaction)
                .FirstOrDefaultAsync(o => o.Id == id && o.UserId == actor.UserId);
            return order ?? throw new NotFoundException("Trajni nalog nije pronađen.");
        }

        public async Task<StandingOrder> CreateAsync(CreateStandingOrderCommand command, Actor actor)
        {
            EnsureCustomer(actor);
            var today = _businessCalendar.Today();
            ValidateSchedule(command.StartDate, command.EndDate, command.MaxExecutions, today);
            if (command.StartDate > today.AddDays(MaxStartDaysAhead))
                throw new BusinessRuleException("Trajni nalog može početi najviše godinu dana unapred.");

            var active = await _context.StandingOrders.CountAsync(o =>
                o.UserId == actor.UserId && (o.Status == StandingOrderStatus.Aktivan || o.Status == StandingOrderStatus.Pauziran));
            if (active >= MaxActivePerUser)
                throw new BusinessRuleException($"Možete imati najviše {MaxActivePerUser} aktivnih trajnih naloga.");

            await _paymentOrders.QuoteAsync(ToPaymentCommand(command, command.StartDate), actor);

            var account = await _context.Accounts.AsNoTracking().FirstAsync(a => a.Id == command.FromAccountId);
            var order = new StandingOrder
            {
                ReferenceNumber = $"TN-{UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3))}",
                UserId = actor.UserId,
                FromAccountId = command.FromAccountId,
                RecipientName = command.RecipientName.Trim(),
                RecipientAccountNumber = SerbianAccountNumber.TryParse(command.RecipientAccountNumber, out var parsed)
                    ? parsed.Formatted
                    : command.RecipientAccountNumber.Trim(),
                RecipientAddress = Blank(command.RecipientAddress),
                Amount = command.Amount,
                Currency = account.Currency,
                PaymentCode = command.PaymentCode,
                Purpose = command.Purpose.Trim(),
                ReferenceModel = Blank(command.ReferenceModel),
                ReferenceNumberOnSlip = Blank(command.ReferenceNumber),
                Frequency = command.Frequency,
                StartDate = command.StartDate,
                EndDate = command.EndDate,
                MaxExecutions = command.MaxExecutions,
                NextExecutionDate = command.StartDate,
                Status = StandingOrderStatus.Aktivan,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            };

            await _context.StandingOrders.AddAsync(order);
            await _notificationPublisher.PublishAsync(Notify(order.UserId, "Trajni nalog je aktiviran",
                $"Trajni nalog {order.ReferenceNumber} za primaoca {order.RecipientName} ({MoneyRules.Format(order.Amount, order.Currency)}) " +
                $"prvi put se izvršava {order.StartDate:dd.MM.yyyy}."));
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task<StandingOrder> UpdateAsync(int id, UpdateStandingOrderCommand command, Actor actor)
        {
            var order = await LoadOwnedAsync(id, actor);
            EnsureOpen(order);
            MoneyRules.EnsureValidAmount(command.Amount);
            ValidateSchedule(order.StartDate, command.EndDate, command.MaxExecutions, _businessCalendar.Today());
            if (command.MaxExecutions.HasValue && command.MaxExecutions.Value < order.OccurrenceIndex)
                throw new BusinessRuleException($"Broj izvršenja ne može biti manji od već obrađenih ({order.OccurrenceIndex}).");

            await _paymentOrders.QuoteAsync(new CreatePaymentOrderCommand(
                order.FromAccountId, order.RecipientName, order.RecipientAccountNumber, order.RecipientAddress, command.Amount,
                order.PaymentCode, command.Purpose, command.ReferenceModel, command.ReferenceNumber,
                MaxDate(order.NextExecutionDate ?? _businessCalendar.Today(), _businessCalendar.Today()), null, null), actor);

            order.Amount = command.Amount;
            order.Purpose = command.Purpose.Trim();
            order.ReferenceModel = Blank(command.ReferenceModel);
            order.ReferenceNumberOnSlip = Blank(command.ReferenceNumber);
            order.EndDate = command.EndDate;
            order.MaxExecutions = command.MaxExecutions;
            order.UpdatedAt = UtcNow;
            RecalculateNext(order, _businessCalendar.Today());
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task<StandingOrder> PauseAsync(int id, Actor actor)
        {
            var order = await LoadOwnedAsync(id, actor);
            if (order.Status != StandingOrderStatus.Aktivan)
                throw new BusinessRuleException("Pauzirati se može samo aktivan trajni nalog.");
            order.Status = StandingOrderStatus.Pauziran;
            order.UpdatedAt = UtcNow;
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task<StandingOrder> ResumeAsync(int id, Actor actor)
        {
            var order = await LoadOwnedAsync(id, actor);
            if (order.Status != StandingOrderStatus.Pauziran)
                throw new BusinessRuleException("Nastaviti se može samo pauziran trajni nalog.");
            order.Status = StandingOrderStatus.Aktivan;
            order.ConsecutiveFailures = 0;
            order.UpdatedAt = UtcNow;
            RecalculateNext(order, _businessCalendar.Today());
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task<StandingOrder> CancelAsync(int id, Actor actor)
        {
            var order = await LoadOwnedAsync(id, actor);
            EnsureOpen(order);
            order.Status = StandingOrderStatus.Otkazan;
            order.NextExecutionDate = null;
            order.UpdatedAt = UtcNow;
            await _notificationPublisher.PublishAsync(Notify(order.UserId, "Trajni nalog je otkazan",
                $"Trajni nalog {order.ReferenceNumber} za primaoca {order.RecipientName} više se neće izvršavati."));
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task<int> ExecuteDueAsync(int batchSize, CancellationToken cancellationToken)
        {
            var today = _businessCalendar.Today();
            var dueIds = await _context.StandingOrders.AsNoTracking()
                .Where(o => o.Status == StandingOrderStatus.Aktivan && o.NextExecutionDate <= today)
                .OrderBy(o => o.NextExecutionDate)
                .ThenBy(o => o.Id)
                .Select(o => o.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            var processed = 0;
            foreach (var id in dueIds)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;
                try
                {
                    if (await ExecuteOneAsync(id, today, cancellationToken))
                        processed++;
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    _logger.LogError(exception, "Izvršenje trajnog naloga {StandingOrderId} nije uspelo", id);
                }
            }
            return processed;
        }

        private async Task<bool> ExecuteOneAsync(int id, DateOnly today, CancellationToken cancellationToken)
        {
            var claim = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                foreach (var tracked in _context.ChangeTracker.Entries<StandingOrder>().Where(e => e.Entity.Id == id).ToList())
                    tracked.State = EntityState.Detached;

                var order = (await _context.StandingOrders
                    .FromSqlInterpolated($"SELECT * FROM standing_orders WHERE Id = {id} AND Status = 'Aktivan' AND NextExecutionDate <= {today} FOR UPDATE SKIP LOCKED")
                    .ToListAsync(cancellationToken)).SingleOrDefault();
                if (order == null)
                    return null;

                var execution = new StandingOrderExecution
                {
                    StandingOrderId = order.Id,
                    ScheduledFor = order.NextExecutionDate!.Value,
                    Status = StandingOrderExecutionStatus.Pokrenut,
                    CreatedAt = UtcNow
                };
                await _context.StandingOrderExecutions.AddAsync(execution);

                order.OccurrenceIndex++;
                order.UpdatedAt = UtcNow;
                RecalculateNext(order, today);
                await _context.SaveChangesAsync(cancellationToken);
                return new Claim(order.Id, execution.Id, execution.ScheduledFor);
            }, cancellationToken);

            if (claim == null)
                return false;

            var snapshot = await _context.StandingOrders.AsNoTracking().FirstAsync(o => o.Id == claim.OrderId, cancellationToken);
            var owner = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == snapshot.UserId, cancellationToken);

            PaymentOrder? payment = null;
            string? failure = null;
            if (!owner.IsActive)
            {
                failure = "Nalog korisnika nije aktivan.";
            }
            else
            {
                try
                {
                    var command = new CreatePaymentOrderCommand(
                        snapshot.FromAccountId, snapshot.RecipientName, snapshot.RecipientAccountNumber, snapshot.RecipientAddress,
                        snapshot.Amount, snapshot.PaymentCode, snapshot.Purpose, snapshot.ReferenceModel, snapshot.ReferenceNumberOnSlip,
                        null, null, $"TN-{snapshot.Id}-{claim.ScheduledFor:yyyyMMdd}", StrongConfirmationProvided: true);
                    payment = (await _paymentOrders.CreateAsync(command, new Actor(snapshot.UserId, UserRole.Korisnik))).Order;
                }
                catch (DomainException exception)
                {
                    failure = exception.Message;
                }
            }

            var order = await _context.StandingOrders.FirstAsync(o => o.Id == claim.OrderId, cancellationToken);
            var execution = await _context.StandingOrderExecutions.FirstAsync(e => e.Id == claim.ExecutionId, cancellationToken);
            execution.CompletedAt = UtcNow;
            order.UpdatedAt = UtcNow;

            if (payment != null)
            {
                execution.Status = StandingOrderExecutionStatus.Izvrsen;
                execution.PaymentOrderId = payment.Id;
                order.ExecutionsCount++;
                order.ConsecutiveFailures = 0;
                order.LastExecutedAt = UtcNow;
                order.LastFailureReason = null;
            }
            else
            {
                var reason = Truncate(failure ?? "Nepoznat razlog.", 300);
                execution.Status = StandingOrderExecutionStatus.Odbijen;
                execution.FailureReason = reason;
                order.ConsecutiveFailures++;
                order.LastFailureReason = reason;

                var paused = order.Status == StandingOrderStatus.Aktivan && order.ConsecutiveFailures >= MaxConsecutiveFailures;
                if (paused)
                    order.Status = StandingOrderStatus.Pauziran;

                await _notificationPublisher.PublishAsync(Notify(order.UserId,
                    paused ? "Trajni nalog je pauziran" : "Trajni nalog nije izvršen",
                    $"Plaćanje po trajnom nalogu {order.ReferenceNumber} za {claim.ScheduledFor:dd.MM.yyyy} nije izvršeno: {reason}" +
                    (paused
                        ? $" Posle {MaxConsecutiveFailures} neuspešna pokušaja zaredom nalog je pauziran. Nastavite ga kada rešite problem."
                        : order.NextExecutionDate.HasValue ? $" Sledeći pokušaj je {order.NextExecutionDate:dd.MM.yyyy}." : string.Empty)));
            }

            if (order.Status == StandingOrderStatus.Zavrsen)
            {
                await _notificationPublisher.PublishAsync(Notify(order.UserId, "Trajni nalog je završen",
                    $"Trajni nalog {order.ReferenceNumber} je obavio sva planirana plaćanja ({order.ExecutionsCount})."));
            }

            await _context.SaveChangesAsync(cancellationToken);
            if (payment != null)
                await _budgetService.TryEvaluateAlertsAsync(order.UserId);
            return true;
        }

        private static void RecalculateNext(StandingOrder order, DateOnly today)
        {
            if (order.Status is StandingOrderStatus.Otkazan)
                return;

            var index = order.OccurrenceIndex;
            var date = StandingOrderSchedule.Occurrence(order.StartDate, order.Frequency, index);
            if (order.Status == StandingOrderStatus.Aktivan)
            {
                while (date < today && StandingOrderSchedule.IsWithinLimits(date, index, order.EndDate, order.MaxExecutions))
                {
                    index++;
                    date = StandingOrderSchedule.Occurrence(order.StartDate, order.Frequency, index);
                }
                order.OccurrenceIndex = Math.Max(order.OccurrenceIndex, index);
            }

            if (StandingOrderSchedule.IsWithinLimits(date, index, order.EndDate, order.MaxExecutions))
            {
                order.NextExecutionDate = date;
                if (order.Status == StandingOrderStatus.Zavrsen)
                    order.Status = StandingOrderStatus.Aktivan;
            }
            else
            {
                order.NextExecutionDate = null;
                order.Status = StandingOrderStatus.Zavrsen;
            }
        }

        private static void ValidateSchedule(DateOnly startDate, DateOnly? endDate, int? maxExecutions, DateOnly today)
        {
            if (endDate.HasValue && endDate.Value < startDate)
                throw new BusinessRuleException("Datum završetka ne može biti pre datuma početka.");
            if (endDate.HasValue && endDate.Value < today)
                throw new BusinessRuleException("Datum završetka ne može biti u prošlosti.");
            if (maxExecutions is < 1 or > 600)
                throw new BusinessRuleException("Broj izvršenja mora biti između 1 i 600.");
        }

        private static void EnsureOpen(StandingOrder order)
        {
            if (order.Status is StandingOrderStatus.Otkazan or StandingOrderStatus.Zavrsen)
                throw new BusinessRuleException("Trajni nalog je otkazan ili završen i ne može se menjati.");
        }

        private async Task<StandingOrder> LoadOwnedAsync(int id, Actor actor)
        {
            EnsureCustomer(actor);
            return await _context.StandingOrders.FirstOrDefaultAsync(o => o.Id == id && o.UserId == actor.UserId)
                ?? throw new NotFoundException("Trajni nalog nije pronađen.");
        }

        private IQueryable<StandingOrder> QueryForUser(int userId) =>
            _context.StandingOrders.AsNoTracking()
                .Include(o => o.FromAccount)
                .Where(o => o.UserId == userId)
                .OrderBy(o => o.Status == StandingOrderStatus.Aktivan ? 0 : o.Status == StandingOrderStatus.Pauziran ? 1 : 2)
                .ThenBy(o => o.NextExecutionDate)
                .ThenByDescending(o => o.CreatedAt);

        private static CreatePaymentOrderCommand ToPaymentCommand(CreateStandingOrderCommand command, DateOnly scheduledFor) =>
            new(command.FromAccountId, command.RecipientName, command.RecipientAccountNumber, command.RecipientAddress, command.Amount,
                command.PaymentCode, command.Purpose, command.ReferenceModel, command.ReferenceNumber, scheduledFor, null, null);

        private static void EnsureCustomer(Actor actor)
        {
            if (!actor.IsCustomer)
                throw new ForbiddenException("Trajnim nalozima upravlja samo vlasnik računa.");
        }

        private Notification Notify(int userId, string title, string message) => new()
        {
            UserId = userId,
            Type = NotificationType.TrajniNalog,
            Title = title,
            Message = message,
            CreatedAt = UtcNow
        };

        private static DateOnly MaxDate(DateOnly first, DateOnly second) => first >= second ? first : second;

        private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];

        private sealed record Claim(int OrderId, int ExecutionId, DateOnly ScheduledFor);
    }
}
