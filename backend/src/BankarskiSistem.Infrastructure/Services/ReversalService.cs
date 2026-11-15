using System.Security.Cryptography;
using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Enums;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;
using BankarskiSistem.Infrastructure.Data;
using BankarskiSistem.Infrastructure.Repositories;
using BankarskiSistem.Infrastructure.Services.Payments;
using Microsoft.EntityFrameworkCore;

namespace BankarskiSistem.Infrastructure.Services
{
    public class ReversalService : IReversalService
    {
        public const int MaxAgeDays = 90;
        private static readonly TransactionType[] ReversibleTypes = [TransactionType.Uplata, TransactionType.Isplata, TransactionType.Transfer];

        private readonly ApplicationDbContext _context;
        private readonly IAccountRepository _accountRepository;
        private readonly LedgerRecorder _ledgerRecorder;
        private readonly IUnitOfWork _unitOfWork;
        private readonly INotificationPublisher _notificationPublisher;
        private readonly TimeProvider _timeProvider;

        public ReversalService(
            ApplicationDbContext context,
            IAccountRepository accountRepository,
            LedgerRecorder ledgerRecorder,
            IUnitOfWork unitOfWork,
            INotificationPublisher notificationPublisher,
            TimeProvider timeProvider)
        {
            _context = context;
            _accountRepository = accountRepository;
            _ledgerRecorder = ledgerRecorder;
            _unitOfWork = unitOfWork;
            _notificationPublisher = notificationPublisher;
            _timeProvider = timeProvider;
        }

        private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

        public async Task<TransactionReversal> RequestAsync(int transactionId, string reason, Actor actor)
        {
            if (!actor.IsStaff)
                throw new ForbiddenException();
            if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 10)
                throw new BusinessRuleException("Navedite razlog storniranja (najmanje 10 karaktera).");

            var original = await _context.Transactions.AsNoTracking().FirstOrDefaultAsync(t => t.Id == transactionId)
                ?? throw new NotFoundException("Transakcija nije pronađena.");
            EnsureReversible(original);
            if (await _context.TransactionReversals.AnyAsync(r => r.OriginalTransactionId == transactionId && r.Status == ReversalStatus.Podnet))
                throw new ConflictException("Za ovu transakciju već postoji zahtev za storniranje koji čeka odobrenje.");

            var request = new TransactionReversal
            {
                ReferenceNumber = $"STO-{UtcNow:yyyyMMdd}-{Convert.ToHexString(RandomNumberGenerator.GetBytes(3))}",
                OriginalTransactionId = transactionId,
                Reason = reason.Trim(),
                Status = ReversalStatus.Podnet,
                RequestedByUserId = actor.UserId,
                CreatedAt = UtcNow
            };
            await _context.TransactionReversals.AddAsync(request);
            await _context.SaveChangesAsync();
            return await LoadAsync(request.Id);
        }

        public async Task<PagedResult<TransactionReversal>> GetPagedAsync(ReversalStatus? status, PageRequest page, Actor actor)
        {
            if (!actor.IsStaff)
                throw new ForbiddenException();
            var query = Query();
            if (status.HasValue)
                query = query.Where(r => r.Status == status.Value);
            return await query.OrderByDescending(r => r.Status == ReversalStatus.Podnet).ThenByDescending(r => r.CreatedAt).ToPagedResultAsync(page);
        }

        public async Task<TransactionReversal> ApproveAsync(int requestId, string? note, Actor actor)
        {
            EnsureAdministrator(actor);
            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
                var request = await LoadPendingAsync(requestId, actor);
                var original = await _context.Transactions.FirstAsync(t => t.Id == request.OriginalTransactionId);
                EnsureReversible(original);

                var entryId = await _context.JournalEntries.Where(e => e.TransactionId == original.Id).Select(e => e.Id).SingleAsync();
                var postings = await _context.Postings.AsNoTracking()
                    .Include(p => p.LedgerAccount)
                    .Where(p => p.JournalEntryId == entryId)
                    .ToListAsync();

                var accountIds = postings.Where(p => p.LedgerAccount!.AccountId.HasValue).Select(p => p.LedgerAccount!.AccountId!.Value).Distinct().ToList();
                var accounts = (await _accountRepository.LockForUpdateAsync(accountIds)).ToDictionary(a => a.Id);
                var lines = new List<LedgerLine>();
                foreach (var posting in postings)
                {
                    var accountId = posting.LedgerAccount!.AccountId;
                    decimal? balanceAfter = null;
                    if (accountId.HasValue)
                    {
                        var account = accounts[accountId.Value];
                        account.Balance -= posting.Amount;
                        account.UpdatedAt = UtcNow;
                        if (account.Balance < 0)
                            throw new BusinessRuleException($"Na računu {account.AccountNumber} nema dovoljno sredstava za storniranje ({MoneyRules.Format(-account.Balance, account.Currency)} nedostaje).");
                        balanceAfter = account.Balance;
                    }
                    lines.Add(new LedgerLine(posting.LedgerAccountId, -posting.Amount, balanceAfter));
                }

                var storno = new Transaction
                {
                    Amount = original.Amount,
                    FeeAmount = original.FeeAmount,
                    Currency = original.Currency,
                    Type = TransactionType.Storno,
                    Status = TransactionStatus.Uspesna,
                    Description = Truncate($"Storno {original.ReferenceNumber}: {request.Reason}", 500),
                    ReferenceNumber = MoneyRules.NewReferenceNumber(UtcNow),
                    PerformedByUserId = actor.UserId,
                    ReversalOfTransactionId = original.Id,
                    CreatedAt = UtcNow
                };
                (storno.FromAccountId, storno.ToAccountId) = original.Type switch
                {
                    TransactionType.Uplata => (original.ToAccountId, (int?)null),
                    TransactionType.Isplata => ((int?)null, original.FromAccountId),
                    _ => (original.ToAccountId, original.FromAccountId)
                };
                if (storno.FromAccountId.HasValue)
                    storno.FromBalanceAfter = accounts[storno.FromAccountId.Value].Balance;
                if (storno.ToAccountId.HasValue)
                    storno.ToBalanceAfter = accounts[storno.ToAccountId.Value].Balance;

                await _ledgerRecorder.RecordAsync(storno, lines.ToArray());

                original.ReversedAt = UtcNow;
                request.Status = ReversalStatus.Izvrsen;
                request.ReversalTransactionId = storno.Id;
                request.ReviewedByUserId = actor.UserId;
                request.ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
                request.ReviewedAt = UtcNow;

                foreach (var account in accounts.Values)
                {
                    await _notificationPublisher.PublishAsync(new Notification
                    {
                        UserId = account.UserId,
                        Type = NotificationType.StatusRacuna,
                        Title = "Transakcija je stornirana",
                        Message = $"Banka je stornirala transakciju {original.ReferenceNumber} od {original.CreatedAt:dd.MM.yyyy} " +
                                  $"({MoneyRules.Format(original.Amount, original.Currency)}). Razlog: {request.Reason}. Novo stanje: {MoneyRules.Format(account.Balance, account.Currency)}.",
                        CreatedAt = UtcNow
                    });
                }
                await _unitOfWork.SaveChangesAsync();
                return true;
            });
            return await LoadAsync(requestId);
        }

        public async Task<TransactionReversal> RejectAsync(int requestId, string note, Actor actor)
        {
            EnsureAdministrator(actor);
            if (string.IsNullOrWhiteSpace(note))
                throw new BusinessRuleException("Razlog odbijanja je obavezan.");
            var request = await LoadPendingAsync(requestId, actor);
            request.Status = ReversalStatus.Odbijen;
            request.ReviewedByUserId = actor.UserId;
            request.ReviewNote = note.Trim();
            request.ReviewedAt = UtcNow;
            await _context.SaveChangesAsync();
            return await LoadAsync(requestId);
        }

        private void EnsureReversible(Transaction transaction)
        {
            if (transaction.Type == TransactionType.EksterniTransfer)
                throw new BusinessRuleException("Plaćanje drugoj banci ne može se stornirati jer je novac poslat u kliring. Potrebno je pokrenuti povraćaj sredstava preko banke primaoca.");
            if (!ReversibleTypes.Contains(transaction.Type))
                throw new BusinessRuleException("Ova vrsta transakcije ne može se stornirati.");
            if (transaction.Status != TransactionStatus.Uspesna)
                throw new BusinessRuleException("Stornirati se može samo uspešna transakcija.");
            if (transaction.ReversedAt != null)
                throw new BusinessRuleException("Transakcija je već stornirana.");
            if (transaction.CreatedAt < UtcNow.AddDays(-MaxAgeDays))
                throw new BusinessRuleException($"Stornirati se mogu samo transakcije iz poslednjih {MaxAgeDays} dana.");
        }

        private async Task<TransactionReversal> LoadPendingAsync(int requestId, Actor actor)
        {
            var request = await _context.TransactionReversals.FirstOrDefaultAsync(r => r.Id == requestId)
                ?? throw new NotFoundException("Zahtev za storniranje nije pronađen.");
            if (request.RequestedByUserId == actor.UserId)
                throw new BusinessRuleException("Ne možete odobriti ili odbiti zahtev koji ste sami podneli. Odluku donosi drugi zaposleni.");
            if (request.Status != ReversalStatus.Podnet)
                throw new BusinessRuleException("O ovom zahtevu je već odlučeno.");
            return request;
        }

        private IQueryable<TransactionReversal> Query() =>
            _context.TransactionReversals.AsNoTracking()
                .Include(r => r.OriginalTransaction).ThenInclude(t => t!.FromAccount)
                .Include(r => r.OriginalTransaction).ThenInclude(t => t!.ToAccount)
                .Include(r => r.RequestedBy)
                .Include(r => r.ReviewedBy);

        private async Task<TransactionReversal> LoadAsync(int id) => await Query().FirstAsync(r => r.Id == id);

        private static void EnsureAdministrator(Actor actor)
        {
            if (!actor.IsAdministrator)
                throw new ForbiddenException("Storniranje odobrava administrator.");
        }

        private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
    }
}
