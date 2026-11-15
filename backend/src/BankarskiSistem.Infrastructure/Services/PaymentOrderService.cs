using BankarskiSistem.Core.Banking;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Core.Settings;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Services.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BankarskiSistem.Infrastructure.Services
{
    public class PaymentOrderService : IPaymentOrderService
    {
        public const string DomesticClearingCurrency = "RSD";
        private const int MaxScheduleDaysAhead = 365;
        private const decimal LargeShareOfBalance = 0.5m;

        private readonly IPaymentOrderRepository _orderRepository;
        private readonly IAccountRepository _accountRepository;
        private readonly IBeneficiaryRepository _beneficiaryRepository;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly ILedgerRepository _ledgerRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly DailyLimitPolicy _dailyLimitPolicy;
        private readonly LedgerRecorder _ledgerRecorder;
        private readonly IFeeService _feeService;
        private readonly IComplianceMonitor _complianceMonitor;
        private readonly IPreferencesService _preferencesService;
        private readonly ApplicationDbContext _context;
        private readonly IBusinessCalendar _businessCalendar;
        private readonly BankingSettings _bankingSettings;
        private readonly CustomerProtectionSettings _protectionSettings;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<PaymentOrderService> _logger;

        public PaymentOrderService(
            IPaymentOrderRepository orderRepository,
            IAccountRepository accountRepository,
            IBeneficiaryRepository beneficiaryRepository,
            INotificationPublisher notificationPublisher,
            ILedgerRepository ledgerRepository,
            IUnitOfWork unitOfWork,
            DailyLimitPolicy dailyLimitPolicy,
            LedgerRecorder ledgerRecorder,
            IFeeService feeService,
            IComplianceMonitor complianceMonitor,
            IPreferencesService preferencesService,
            ApplicationDbContext context,
            IBusinessCalendar businessCalendar,
            IOptions<BankingSettings> bankingSettings,
            IOptions<CustomerProtectionSettings> protectionSettings,
            TimeProvider timeProvider,
            ILogger<PaymentOrderService> logger)
        {
            _orderRepository = orderRepository;
            _accountRepository = accountRepository;
            _beneficiaryRepository = beneficiaryRepository;
            _notificationPublisher = notificationPublisher;
            _ledgerRepository = ledgerRepository;
            _unitOfWork = unitOfWork;
            _dailyLimitPolicy = dailyLimitPolicy;
            _ledgerRecorder = ledgerRecorder;
            _feeService = feeService;
            _complianceMonitor = complianceMonitor;
            _preferencesService = preferencesService;
            _context = context;
            _businessCalendar = businessCalendar;
            _bankingSettings = bankingSettings.Value;
            _protectionSettings = protectionSettings.Value;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        private const string SavingsOutgoingMessage =
            "Sa štednog računa novac se može prebaciti samo na sopstveni račun. Za plaćanje prvo prebacite novac na tekući račun.";

        public async Task<PaymentQuote> QuoteAsync(CreatePaymentOrderCommand command, Actor actor)
        {
            var prepared = await PrepareAsync(command, actor, includeDailyLimit: true);
            return prepared.ToQuote();
        }

        public async Task<PaymentOrderResult> CreateAsync(CreatePaymentOrderCommand command, Actor actor)
        {
            if (!actor.IsCustomer)
                throw new ForbiddenException("Platni nalog može kreirati samo vlasnik računa.");

            if (command.IdempotencyKey != null)
            {
                var existing = await _orderRepository.GetByIdempotencyKeyAsync(actor.UserId, command.IdempotencyKey);
                if (existing != null)
                    return new PaymentOrderResult(EnsureSameOrder(existing, command), true);
            }

            var prepared = await PrepareAsync(command, actor, includeDailyLimit: false);

            if (prepared.RequiresStrongConfirmation && !command.StrongConfirmationProvided)
                throw new ReauthenticationRequiredException(
                    "Zbog iznosa ili novog primaoca potrebno je da ponovo potvrdite lozinku pre slanja naloga.");

            var order = new PaymentOrder
            {
                UserId = actor.UserId,
                FromAccountId = prepared.Source.Id,
                ToAccountId = prepared.RecipientAccountId,
                RecipientName = command.RecipientName.Trim(),
                RecipientAccountNumber = prepared.RecipientNumber.Formatted,
                RecipientAddress = NullIfBlank(command.RecipientAddress),
                Amount = command.Amount,
                Currency = prepared.Source.Currency,
                FeeAmount = prepared.Fee.Fee,
                FeeScheduleId = prepared.Fee.Entry?.Id,
                PaymentCode = command.PaymentCode,
                Purpose = command.Purpose.Trim(),
                ReferenceModel = NullIfBlank(command.ReferenceModel),
                ReferenceNumber = NullIfBlank(command.ReferenceNumber),
                ScheduledFor = command.ScheduledFor,
                Status = PaymentOrderStatus.Zakazan,
                IdempotencyKey = command.IdempotencyKey,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            };

            ComplianceCheck? complianceCheck = null;
            try
            {
                await _unitOfWork.ExecuteInTransactionAsync(async () =>
                {
                    if (prepared.IsScheduled)
                        await _orderRepository.AddAsync(order);
                    else
                        complianceCheck = await ExecuteOrderAsync(order, isNewOrder: true);

                    await RememberBeneficiaryAsync(actor.UserId, order, command.SaveAsBeneficiary);
                    return order;
                });
            }
            catch (DuplicateEntryException) when (command.IdempotencyKey != null)
            {
                var existing = await _orderRepository.GetByIdempotencyKeyAsync(actor.UserId, command.IdempotencyKey)
                    ?? throw new ConflictException("Zahtev sa istim ključem je već u obradi. Pokušajte ponovo.");
                return new PaymentOrderResult(EnsureSameOrder(existing, command), true);
            }

            await RunComplianceAsync(complianceCheck);
            return new PaymentOrderResult(await GetAsync(order.Id, actor), false);
        }

        public async Task<PaymentOrder> CancelAsync(int orderId, Actor actor)
        {
            var order = await GetAsync(orderId, actor);
            if (!order.CanBeCancelled)
                throw new BusinessRuleException("Može se otkazati samo zakazan nalog koji još nije izvršen.");

            var cancelled = await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var locked = await _orderRepository.TryLockDueOrderAsync(orderId);
                if (locked == null || locked.UserId != actor.UserId)
                    return false;

                locked.Status = PaymentOrderStatus.Otkazan;
                locked.UpdatedAt = UtcNow;
                return true;
            });

            if (!cancelled)
                throw new ConflictException("Nalog je upravo u izvršenju i više se ne može otkazati.");

            return await GetAsync(orderId, actor);
        }

        public async Task<PaymentOrder> GetAsync(int orderId, Actor actor) =>
            await _orderRepository.GetForUserAsync(actor.UserId, orderId)
            ?? throw new NotFoundException("Platni nalog nije pronađen.");

        public Task<PagedResult<PaymentOrder>> GetPagedAsync(PaymentOrderFilter filter, PageRequest page, Actor actor) =>
            _orderRepository.GetPagedForUserAsync(actor.UserId, filter, page);

        public async Task<int> ExecuteDueOrdersAsync(int batchSize, CancellationToken cancellationToken)
        {
            var dueIds = await _orderRepository.GetDueOrderIdsAsync(_businessCalendar.Today(), batchSize);
            var processed = 0;

            foreach (var orderId in dueIds)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    ComplianceCheck? complianceCheck = null;
                    var executed = await _unitOfWork.ExecuteInTransactionAsync(async () =>
                    {
                        var order = await _orderRepository.TryLockDueOrderAsync(orderId);
                        if (order == null)
                            return false;
                        complianceCheck = await ExecuteOrderAsync(order, isNewOrder: false);
                        return true;
                    }, cancellationToken);

                    if (executed)
                    {
                        processed++;
                        await RunComplianceAsync(complianceCheck);
                    }
                }
                catch (DomainException exception)
                {
                    await RejectAsync(orderId, exception.Message, cancellationToken);
                    processed++;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Izvršenje zakazanog naloga {OrderId} nije uspelo", orderId);
                }
            }

            return processed;
        }

        private async Task RunComplianceAsync(ComplianceCheck? check)
        {
            if (check == null)
                return;

            try
            {
                await _complianceMonitor.EvaluateAsync(check.Transaction, check.Account, check.RecipientAccountNumber);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "AML provera transakcije {TransactionId} nije uspela", check.Transaction.Id);
            }
        }

        private async Task<PreparedPayment> PrepareAsync(CreatePaymentOrderCommand command, Actor actor, bool includeDailyLimit)
        {
            if (!actor.IsCustomer)
                throw new ForbiddenException("Platni nalog može kreirati samo vlasnik računa.");

            MoneyRules.EnsureValidAmount(command.Amount);
            ValidateOrderDetails(command);

            if (!SerbianAccountNumber.TryParse(command.RecipientAccountNumber, out var recipientNumber))
                throw new BusinessRuleException("Broj računa primaoca nije u ispravnom formatu (npr. 205-0000001234567-89).");

            var source = await _accountRepository.GetByIdAsync(command.FromAccountId)
                ?? throw new NotFoundException("Račun platioca nije pronađen.");
            AccountAccessPolicy.EnsureOwner(source, actor);

            var isInternal = recipientNumber.BankCode == _bankingSettings.BankCode;
            int? recipientAccountId = null;
            var isOwnTransfer = false;

            if (isInternal)
            {
                var recipient = await _accountRepository.FindByNumberAsync(recipientNumber.Formatted)
                    ?? throw new NotFoundException("Račun primaoca ne postoji u našoj banci.");
                if (recipient.Id == source.Id)
                    throw new BusinessRuleException("Račun platioca i primaoca ne mogu biti isti.");
                if (recipient.Currency != source.Currency)
                    throw new BusinessRuleException(
                        $"Prenos između računa u različitim valutama ({source.Currency} i {recipient.Currency}) nije moguć bez konverzije.");
                if (source.Kind == AccountKind.Stednja && recipient.UserId != source.UserId)
                    throw new BusinessRuleException(SavingsOutgoingMessage);
                recipientAccountId = recipient.Id;
                isOwnTransfer = recipient.UserId == source.UserId;
            }
            else
            {
                if (source.Kind == AccountKind.Stednja)
                    throw new BusinessRuleException(SavingsOutgoingMessage);
                if (!recipientNumber.HasValidControlDigits)
                    throw new BusinessRuleException("Kontrolni broj računa primaoca nije ispravan. Proverite broj računa.");
                if (source.Currency != DomesticClearingCurrency)
                    throw new BusinessRuleException("Plaćanja ka drugim bankama moguća su samo sa dinarskog računa.");
            }

            var today = _businessCalendar.Today();
            if (command.ScheduledFor < today)
                throw new BusinessRuleException("Datum izvršenja ne može biti u prošlosti.");
            if (command.ScheduledFor > today.AddDays(MaxScheduleDaysAhead))
                throw new BusinessRuleException("Nalog se može zakazati najviše godinu dana unapred.");

            var fee = await _feeService.QuoteAsync(
                isInternal ? FeeAppliesTo.InterniNalog : FeeAppliesTo.EksterniNalog, command.Amount, source.Currency);

            var isKnownRecipient = await IsKnownRecipientAsync(actor.UserId, recipientNumber.Formatted);
            var remainingDailyLimit = includeDailyLimit ? await _dailyLimitPolicy.GetRemainingTodayAsync(source) : decimal.MaxValue;
            var preferences = await _preferencesService.GetAsync(actor.UserId);
            var bankThreshold = _protectionSettings.GetStrongConfirmationThreshold(source.Currency);

            var requiresStrongConfirmation =
                (bankThreshold.HasValue && command.Amount >= bankThreshold.Value && !isKnownRecipient)
                || (preferences.ExtraConfirmationThreshold.HasValue && command.Amount >= preferences.ExtraConfirmationThreshold.Value);

            return new PreparedPayment(
                source,
                recipientNumber,
                isInternal,
                recipientAccountId,
                command.Amount,
                fee,
                isKnownRecipient,
                command.ScheduledFor > today,
                command.ScheduledFor,
                requiresStrongConfirmation,
                remainingDailyLimit,
                isOwnTransfer);
        }

        private async Task<bool> IsKnownRecipientAsync(int userId, string accountNumber) =>
            await _context.PaymentOrders.AnyAsync(p => p.UserId == userId && p.RecipientAccountNumber == accountNumber && p.TransactionId != null)
            || await _context.Beneficiaries.AnyAsync(b => b.UserId == userId && b.AccountNumber == accountNumber)
            || await _context.Accounts.AnyAsync(a => a.UserId == userId && a.AccountNumber == accountNumber);

        private async Task<ComplianceCheck> ExecuteOrderAsync(PaymentOrder order, bool isNewOrder)
        {
            var accountIds = order.ToAccountId.HasValue
                ? new[] { order.FromAccountId, order.ToAccountId.Value }
                : new[] { order.FromAccountId };

            var locked = await _accountRepository.LockForUpdateAsync(accountIds);
            var from = locked.SingleOrDefault(a => a.Id == order.FromAccountId)
                ?? throw new NotFoundException("Račun platioca nije pronađen.");

            if (from.UserId != order.UserId)
                throw new ForbiddenException("Nemate pristup računu platioca.");

            MoneyRules.EnsureActive(from, "Nalog nije izvršen jer račun platioca nije aktivan.");

            var ledgerIds = await _ledgerRepository.GetCustomerLedgerAccountIdsAsync(accountIds);
            Account? to = null;
            int counterpartyLedgerId;

            if (order.ToAccountId.HasValue)
            {
                to = locked.SingleOrDefault(a => a.Id == order.ToAccountId.Value)
                    ?? throw new NotFoundException("Račun primaoca nije pronađen.");
                MoneyRules.EnsureActive(to, "Nalog nije izvršen jer račun primaoca nije aktivan.");
                if (to.Currency != from.Currency)
                    throw new BusinessRuleException(
                        $"Prenos između računa u različitim valutama ({from.Currency} i {to.Currency}) nije moguć bez konverzije.");
                counterpartyLedgerId = ledgerIds[to.Id];
            }
            else
            {
                counterpartyLedgerId = await _ledgerRepository.GetSystemLedgerAccountIdAsync(LedgerAccountType.Kliring, from.Currency);
            }

            var totalDebit = order.Amount + order.FeeAmount;
            if (to == null || to.UserId != from.UserId)
                await _dailyLimitPolicy.EnsureWithinLimitsAsync(from, order.Amount);
            MoneyRules.EnsureSufficientFunds(from, totalDebit);

            from.Balance -= totalDebit;
            from.UpdatedAt = UtcNow;
            if (to != null)
            {
                to.Balance += order.Amount;
                to.UpdatedAt = UtcNow;
            }

            var transaction = new Transaction
            {
                FromAccountId = from.Id,
                ToAccountId = to?.Id,
                Amount = order.Amount,
                FeeAmount = order.FeeAmount,
                Currency = from.Currency,
                FromBalanceAfter = from.Balance,
                ToBalanceAfter = to?.Balance,
                Type = to != null ? TransactionType.Transfer : TransactionType.EksterniTransfer,
                Status = TransactionStatus.Uspesna,
                Description = order.Purpose,
                ReferenceNumber = MoneyRules.NewReferenceNumber(UtcNow),
                PerformedByUserId = order.UserId,
                CreatedAt = UtcNow
            };

            var lines = new List<LedgerLine>
            {
                new(ledgerIds[from.Id], -totalDebit, from.Balance),
                new(counterpartyLedgerId, order.Amount, to?.Balance)
            };
            if (order.FeeAmount > 0)
                lines.Add(new LedgerLine(
                    await _ledgerRepository.GetSystemLedgerAccountIdAsync(LedgerAccountType.Provizije, from.Currency), order.FeeAmount));

            await _ledgerRecorder.RecordAsync(transaction, lines.ToArray());

            order.TransactionId = transaction.Id;
            order.Status = to != null ? PaymentOrderStatus.Izvrsen : PaymentOrderStatus.UObradi;
            order.ExecutedAt = UtcNow;
            order.UpdatedAt = UtcNow;

            if (isNewOrder)
                await _orderRepository.AddAsync(order);

            var notifications = new List<Notification> { NotificationFactory.PaymentSent(from, order, transaction, UtcNow) };
            if (to != null && to.UserId != from.UserId)
                notifications.Add(NotificationFactory.PaymentReceived(from, to, order, transaction, UtcNow));
            await _notificationPublisher.PublishAsync(notifications.ToArray());

            return new ComplianceCheck(transaction, from, order.RecipientAccountNumber);
        }

        private async Task RejectAsync(int orderId, string reason, CancellationToken cancellationToken)
        {
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var order = await _orderRepository.TryLockDueOrderAsync(orderId);
                if (order == null)
                    return false;

                order.Status = PaymentOrderStatus.Odbijen;
                order.RejectionReason = reason.Length > 255 ? reason[..255] : reason;
                order.UpdatedAt = UtcNow;
                await _notificationPublisher.PublishAsync(NotificationFactory.PaymentRejected(order, UtcNow));
                return true;
            }, cancellationToken);
        }

        private async Task RememberBeneficiaryAsync(int userId, PaymentOrder order, string? nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                return;

            var trimmed = nickname.Trim();
            var beneficiary = await _beneficiaryRepository.FindByNicknameAsync(userId, trimmed);
            if (beneficiary == null)
            {
                beneficiary = new Beneficiary { UserId = userId, Nickname = trimmed, CreatedAt = UtcNow };
                await _beneficiaryRepository.AddAsync(beneficiary);
            }

            beneficiary.RecipientName = order.RecipientName;
            beneficiary.AccountNumber = order.RecipientAccountNumber;
            beneficiary.RecipientAddress = order.RecipientAddress;
            beneficiary.PaymentCode = order.PaymentCode;
            beneficiary.Purpose = order.Purpose;
            beneficiary.ReferenceModel = order.ReferenceModel;
            beneficiary.ReferenceNumber = order.ReferenceNumber;
            beneficiary.UpdatedAt = UtcNow;
            beneficiary.LastUsedAt = UtcNow;
        }

        private static void ValidateOrderDetails(CreatePaymentOrderCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.RecipientName))
                throw new BusinessRuleException("Naziv primaoca je obavezan.");
            if (string.IsNullOrWhiteSpace(command.Purpose))
                throw new BusinessRuleException("Svrha plaćanja je obavezna.");
            if (!PaymentCodes.IsValidFormat(command.PaymentCode))
                throw new BusinessRuleException("Šifra plaćanja mora biti trocifren broj koji počinje sa 1 ili 2.");

            var referenceError = PaymentReference.Validate(NullIfBlank(command.ReferenceModel), NullIfBlank(command.ReferenceNumber));
            if (referenceError != null)
                throw new BusinessRuleException(referenceError);
        }

        private static PaymentOrder EnsureSameOrder(PaymentOrder existing, CreatePaymentOrderCommand command)
        {
            var sameRecipient = SerbianAccountNumber.TryParse(command.RecipientAccountNumber, out var recipient)
                                && existing.RecipientAccountNumber == recipient.Formatted;
            if (existing.FromAccountId != command.FromAccountId || existing.Amount != command.Amount || !sameRecipient)
                throw new ConflictException("Ključ idempotentnosti je već iskorišćen za drugačiji nalog.");
            return existing;
        }

        private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        private sealed record ComplianceCheck(Transaction Transaction, Account Account, string RecipientAccountNumber);

        private sealed record PreparedPayment(
            Account Source,
            SerbianAccountNumber RecipientNumber,
            bool IsInternal,
            int? RecipientAccountId,
            decimal Amount,
            FeeQuote Fee,
            bool IsKnownRecipient,
            bool IsScheduled,
            DateOnly? ScheduledFor,
            bool RequiresStrongConfirmation,
            decimal RemainingDailyLimit,
            bool IsOwnTransfer)
        {
            public PaymentQuote ToQuote()
            {
                var total = Amount + Fee.Fee;
                var balanceAfter = Source.Balance - total;
                var warnings = new List<string>();

                if (!IsKnownRecipient)
                    warnings.Add("Prvi put plaćate ovom primaocu. Proverite broj računa i naziv primaoca pre potvrde.");
                if (!IsInternal)
                    warnings.Add("Nalog ka drugoj banci odmah se skida sa računa i šalje u kliring. Posle slanja ne može se otkazati.");
                if (balanceAfter < 0)
                    warnings.Add("Na računu trenutno nema dovoljno sredstava za ovaj iznos sa naknadom.");
                else if (Source.Balance > 0 && total > Source.Balance * LargeShareOfBalance)
                    warnings.Add("Plaćanje iznosi više od polovine raspoloživog stanja na računu.");
                if (!IsScheduled && !IsOwnTransfer && Amount > RemainingDailyLimit)
                    warnings.Add($"Iznos prelazi preostali dnevni limit za danas ({MoneyRules.Format(RemainingDailyLimit, Source.Currency)}). Nalog neće biti izvršen.");
                if (IsScheduled)
                    warnings.Add($"Nalog će biti izvršen {ScheduledFor:dd.MM.yyyy} ako na računu tada bude dovoljno sredstava. Do tada ga možete otkazati.");
                if (RequiresStrongConfirmation)
                    warnings.Add("Zbog iznosa ili novog primaoca biće potrebna dodatna potvrda lozinkom.");

                return new PaymentQuote(
                    Amount,
                    Fee.Fee,
                    total,
                    Source.Currency,
                    Source.Balance,
                    balanceAfter,
                    IsInternal,
                    IsKnownRecipient,
                    IsScheduled,
                    Fee.Entry?.Name,
                    Fee.Entry?.Description,
                    RequiresStrongConfirmation,
                    RemainingDailyLimit,
                    warnings);
            }
        }
    }
}
