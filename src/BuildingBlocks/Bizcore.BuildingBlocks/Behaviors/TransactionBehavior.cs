using Bizcore.BuildingBlocks.Abstractions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Bizcore.BuildingBlocks.Behaviors;

/// <summary>
/// MediatR pipeline behavior that automatically wraps transactional commands in a database transaction.
/// Uses IUnitOfWork abstraction to avoid direct DbContext dependency.
/// </summary>
/// <typeparam name="TRequest">The request type</typeparam>
/// <typeparam name="TResponse">The response type</typeparam>
public class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;

    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var typeName = request.GetType().Name;

        // Skip transaction for queries or non-transactional commands
        if (!IsTransactionalCommand(request))
        {
            _logger.LogDebug("Skipping transaction for {CommandName} (not transactional)", typeName);
            return await next();
        }

        _logger.LogInformation("Begin transaction for {CommandName}", typeName);

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);
        var committed = false;
        try
        {
            var response = await next();

            await _unitOfWork.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Committed transaction for {CommandName}. TransactionId: {TransactionId}",
                typeName,
                transaction.TransactionId
            );
            committed = true;
            return response;
        }
        catch (Exception ex)
        {
            if (!committed)
            {
                await _unitOfWork.RollbackAsync(cancellationToken);
            }
            _logger.LogError(
                ex,
                "Rolled back transaction for {CommandName}. TransactionId: {TransactionId}",
                typeName,
                transaction.TransactionId
            );

            throw;
        }
    }

    private static bool IsTransactionalCommand(TRequest request)
    {
        // Option 1: Marker interface (explicit)
        if (request is ITransactionalCommand)
        {
            return true;
        }

        // Option 2: Convention-based (commands need transaction, queries don't)
        var typeName = request.GetType().Name;
        return typeName.EndsWith("Command") && !typeName.EndsWith("Query");
    }
}
