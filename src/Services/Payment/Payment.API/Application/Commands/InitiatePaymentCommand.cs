using Bizcore.BuildingBlocks.Abstractions;
using MediatR;

namespace Payment.API.Application.Commands;

/// <summary>
/// Command to initiate a payment with idempotency support.
/// This command is transactional and will be wrapped in a database transaction.
/// </summary>
public record InitiatePaymentCommand(
    Guid InvoiceId,
    decimal Amount,
    string PaymentMethod,
    string IdempotencyKey
) : IRequest<InitiatePaymentResult>, ITransactionalCommand;

public record InitiatePaymentResult(
    bool Accepted,
    Guid? PaymentId,
    string? ErrorReason,
    object? CachedResponse = null,
    int? StatusCode = null
);
