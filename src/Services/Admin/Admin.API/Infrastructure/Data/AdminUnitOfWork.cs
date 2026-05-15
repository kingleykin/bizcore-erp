using Bizcore.BuildingBlocks.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Admin.API.Infrastructure.Data;

/// <summary>
/// Unit of Work implementation for Admin service.
/// Used by TransactionBehavior to manage DB transactions for write commands.
/// Auth commands (Login/Logout/Refresh) manage their own saves and are excluded.
/// </summary>
public class AdminUnitOfWork : IUnitOfWork
{
    private readonly AdminDbContext _context;
    private readonly ILogger<AdminUnitOfWork> _logger;
    private IDbContextTransaction? _currentTransaction;

    public AdminUnitOfWork(AdminDbContext context, ILogger<AdminUnitOfWork> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
            throw new InvalidOperationException("Transaction already started");

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return _currentTransaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
            throw new InvalidOperationException("No active transaction");

        try
        {
            _logger.LogDebug("Committing Admin transaction. SaveChangesAsync starting...");
            var count = await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("SaveChangesAsync completed. Records affected: {Count}", count);
            await _currentTransaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Admin transaction committed.");
        }
        catch
        {
            await RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null) return;
        try
        {
            await _currentTransaction.RollbackAsync(cancellationToken);
        }
        finally
        {
            _currentTransaction?.Dispose();
            _currentTransaction = null;
        }
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}
