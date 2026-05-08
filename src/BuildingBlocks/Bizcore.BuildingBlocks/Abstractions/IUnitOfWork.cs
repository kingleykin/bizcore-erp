using Microsoft.EntityFrameworkCore.Storage;

namespace Bizcore.BuildingBlocks.Abstractions;

/// <summary>
/// Unit of Work abstraction for managing database transactions.
/// Each service should implement this interface with its specific DbContext.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Begins a new database transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The database transaction</returns>
    /// <exception cref="InvalidOperationException">Thrown if a transaction is already active</exception>
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the current transaction and saves all changes.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="InvalidOperationException">Thrown if no transaction is active</exception>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rolls back the current transaction.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves changes to the database without committing the transaction.
    /// Use this when you need to flush changes within a transaction (e.g., for unique constraint checks).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of state entries written to the database</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
