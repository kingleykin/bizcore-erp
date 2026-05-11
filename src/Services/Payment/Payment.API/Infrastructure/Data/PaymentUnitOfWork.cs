using Bizcore.BuildingBlocks.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Payment.API.Infrastructure.Data;

/// <summary>
/// Unit of Work implementation for Payment service.
/// Manages transactions for PaymentDbContext (AppDbContext).
/// </summary>
public class PaymentUnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly ILogger<PaymentUnitOfWork> _logger;
    private IDbContextTransaction? _currentTransaction;

    public PaymentUnitOfWork(AppDbContext context, ILogger<PaymentUnitOfWork> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        if (_currentTransaction != null)
        {
            throw new InvalidOperationException("Transaction already started");
        }

        _currentTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        return _currentTransaction;
    }

    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_currentTransaction == null)
        {
            throw new InvalidOperationException("No active transaction");
        }

        try
        {
            _logger.LogDebug("Committing transaction. SaveChangesAsync starting...");
            var savedCount = await _context.SaveChangesAsync(cancellationToken);
            _logger.LogDebug("SaveChangesAsync completed. Records affected: {Count}", savedCount);
            
            await _currentTransaction.CommitAsync(cancellationToken);
            _logger.LogDebug("Transaction committed successfully.");
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
        if (_currentTransaction == null)
        {
            return;
        }

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
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
