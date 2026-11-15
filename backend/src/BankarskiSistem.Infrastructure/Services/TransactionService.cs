using BankarskiSistem.Core.Entities;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using BankarskiSistem.Core.Models;

namespace BankarskiSistem.Infrastructure.Services
{
    public class TransactionService : ITransactionService
    {
        private readonly ITransactionRepository _transactionRepository;
        private readonly IAccountRepository _accountRepository;

        public TransactionService(
            ITransactionRepository transactionRepository,
            IAccountRepository accountRepository)
        {
            _transactionRepository = transactionRepository;
            _accountRepository = accountRepository;
        }

        public async Task<PagedResult<Transaction>> GetPagedAsync(TransactionFilter filter, PageRequest page, Actor actor)
        {
            if (actor.IsCustomer)
            {
                if (filter.AccountId.HasValue)
                {
                    var account = await _accountRepository.GetByIdAsync(filter.AccountId.Value)
                        ?? throw new NotFoundException("Račun nije pronađen.");
                    AccountAccessPolicy.EnsureOwner(account, actor);
                }
                filter = filter with { OwnerUserId = actor.UserId };
            }

            return await _transactionRepository.GetPagedAsync(filter, page);
        }

        public async Task<CursorPage<Transaction>> GetCursorPageAsync(TransactionFilter filter, string? cursor, int limit, Actor actor)
        {
            var safeLimit = Math.Clamp(limit, 1, PageRequest.MaxPageSize);

            if (actor.IsCustomer)
            {
                if (filter.AccountId.HasValue)
                {
                    var account = await _accountRepository.GetByIdAsync(filter.AccountId.Value)
                        ?? throw new NotFoundException("Račun nije pronađen.");
                    AccountAccessPolicy.EnsureOwner(account, actor);
                }
                filter = filter with { OwnerUserId = actor.UserId };
            }

            return await _transactionRepository.GetCursorPageAsync(filter, cursor, safeLimit);
        }

        public async Task<Transaction> GetByIdAsync(int id, Actor actor)
        {
            var transaction = await _transactionRepository.GetByIdAsync(id)
                ?? throw new NotFoundException($"Transakcija sa ID {id} nije pronađena.");
            EnsureCanView(transaction, actor);
            return transaction;
        }

        public async Task<Transaction> GetByReferenceAsync(string referenceNumber, Actor actor)
        {
            var transaction = await _transactionRepository.GetByReferenceAsync(referenceNumber.Trim())
                ?? throw new NotFoundException($"Transakcija {referenceNumber} nije pronađena.");
            EnsureCanView(transaction, actor);
            return transaction;
        }

        private static void EnsureCanView(Transaction transaction, Actor actor)
        {
            if (actor.IsStaff)
                return;

            var isParticipant = transaction.FromAccount?.UserId == actor.UserId
                                || transaction.ToAccount?.UserId == actor.UserId;
            if (!isParticipant)
                throw new NotFoundException("Transakcija nije pronađena.");
        }
    }
}
