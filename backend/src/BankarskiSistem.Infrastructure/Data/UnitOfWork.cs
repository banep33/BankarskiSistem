using System.Data;
using BankarskiSistem.Core.Exceptions;
using BankarskiSistem.Core.Interfaces;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

namespace BankarskiSistem.Infrastructure.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private const int MaxDeadlockRetries = 3;

        private readonly ApplicationDbContext _context;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (FindMySqlError(exception) is { } mySqlError)
            {
                _context.ChangeTracker.Clear();
                throw TranslateDatabaseError(mySqlError, exception);
            }
        }

        public async Task<T> ExecuteInTransactionAsync<T>(
            Func<Task<T>> operation, CancellationToken cancellationToken = default)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await using var transaction = await _context.Database
                        .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

                    var result = await operation();
                    await SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return result;
                }
                catch (Exception exception) when (IsTransientLockError(exception) && attempt < MaxDeadlockRetries)
                {
                    _context.ChangeTracker.Clear();
                }
                catch
                {
                    _context.ChangeTracker.Clear();
                    throw;
                }
            }
        }

        private static bool IsTransientLockError(Exception exception) =>
            FindMySqlError(exception)?.ErrorCode is MySqlErrorCode.LockDeadlock or MySqlErrorCode.LockWaitTimeout;

        private static MySqlException? FindMySqlError(Exception exception)
        {
            for (var current = exception; current != null; current = current.InnerException)
            {
                if (current is MySqlException mySqlException)
                    return mySqlException;
            }
            return null;
        }

        private const int CheckConstraintViolatedCode = 3819;

        private static Exception TranslateDatabaseError(MySqlException error, Exception original)
        {
            if ((int)error.ErrorCode == CheckConstraintViolatedCode)
                return new BusinessRuleException("Operacija krši pravila integriteta podataka.");

            return error.ErrorCode switch
            {
                MySqlErrorCode.DuplicateKeyEntry =>
                    new DuplicateEntryException("Zapis sa istim jedinstvenim podacima već postoji."),
                MySqlErrorCode.RowIsReferenced2 or MySqlErrorCode.RowIsReferenced =>
                    new ConflictException("Zapis je povezan sa drugim podacima i ne može se obrisati."),
                _ => original
            };
        }
    }
}
