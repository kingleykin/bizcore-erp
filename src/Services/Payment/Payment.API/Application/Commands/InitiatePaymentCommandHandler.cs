using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Payment.API.Application.Services;
using Payment.API.Domain.Entities;
using Payment.API.Infrastructure.Data;

namespace Payment.API.Application.Commands;

/// <summary>
/// Handler for InitiatePaymentCommand.
/// Creates a payment with Processing status and publishes PaymentInitiatedEvent.
/// Transaction is managed by TransactionBehavior pipeline.
/// </summary>
public class InitiatePaymentCommandHandler : IRequestHandler<InitiatePaymentCommand, InitiatePaymentResult>
{
    private readonly AppDbContext _context;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IIdempotencyService _idempotencyService;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<InitiatePaymentCommandHandler> _logger;

    public InitiatePaymentCommandHandler(
        AppDbContext context,
        IPublishEndpoint publishEndpoint,
        IIdempotencyService idempotencyService,
        IAuditPublisher audit,
        ILogger<InitiatePaymentCommandHandler> logger)
    {
        _context = context;
        _publishEndpoint = publishEndpoint;
        _idempotencyService = idempotencyService;
        _audit = audit;
        _logger = logger;
    }

    public async Task<InitiatePaymentResult> Handle(
        InitiatePaymentCommand request,
        CancellationToken cancellationToken)
    {
        // Validate idempotency key
        if (string.IsNullOrEmpty(request.IdempotencyKey))
        {
            return new InitiatePaymentResult(false, null, "Idempotency key is required.");
        }


        // Check if invoice exists in Payment service's read model
        var invoice = await _context.Invoices.FirstOrDefaultAsync(
            i => i.Id == request.InvoiceId,
            cancellationToken
        );

        if (invoice == null)
        {
            _logger.LogWarning(
                "Invoice not found in Payment read model. InvoiceId: {InvoiceId}",
                request.InvoiceId
            );
            return new InitiatePaymentResult(false, null, "Invoice not found.");
        }

        // Generate PaymentId before idempotency check
        var paymentId = Guid.NewGuid();

        // Check idempotency (database-backed, thread-safe)
        var idempotencyCheck = await _idempotencyService.CheckOrCreateAsync(
            request.IdempotencyKey,
            new { request.InvoiceId, request.Amount, request.PaymentMethod },
            paymentId,
            TimeSpan.FromMinutes(30)
        );

        if (!idempotencyCheck.IsNew)
        {
            // Duplicate request detected
            if (idempotencyCheck.ConflictReason != null)
            {
                _logger.LogWarning(
                    "Idempotency conflict. Key: {Key}, Reason: {Reason}",
                    request.IdempotencyKey,
                    idempotencyCheck.ConflictReason
                );
                return new InitiatePaymentResult(
                    false,
                    null,
                    idempotencyCheck.ConflictReason
                );
            }

            // Return cached response
            _logger.LogInformation(
                "Duplicate request detected. Key: {Key}, PaymentId: {PaymentId}",
                request.IdempotencyKey,
                idempotencyCheck.PaymentId
            );

            return new InitiatePaymentResult(
                true,
                idempotencyCheck.PaymentId,
                null,
                idempotencyCheck.CachedResponse,
                idempotencyCheck.StatusCode
            );
        }

        // Create payment with Processing status
        var payment = new Payment.API.Domain.Entities.Payment
        {
            Id = paymentId,
            InvoiceId = request.InvoiceId,
            Amount = request.Amount,
            PaymentMethod = request.PaymentMethod,
            PaymentDate = DateTime.UtcNow,
            Status = PaymentStatus.Processing,
            IdempotencyKey = request.IdempotencyKey
        };

        _context.Payments.Add(payment);

        // Publish event INSIDE transaction (saved to Outbox)
        _logger.LogDebug("Publishing IPaymentInitiatedEvent to Outbox. PaymentId: {PaymentId}", payment.Id);

        await _publishEndpoint.Publish<IPaymentInitiatedEvent>(new
        {
            PaymentId = payment.Id,
            InvoiceId = payment.InvoiceId,
            Amount = payment.Amount,
            CustomerId = invoice.CustomerId,
            IdempotencyKey = request.IdempotencyKey,
            InitiatedAt = payment.PaymentDate
        }, cancellationToken);

        _logger.LogDebug("IPaymentInitiatedEvent publish call completed. Waiting for transaction commit.");

        // Publish Explicit Audit Log
        await _audit.PublishAsync(
            AuditActions.Payment.Initiated,
            entityType: "Payment",
            entityId: payment.Id.ToString(),
            after: new { payment.Id, payment.InvoiceId, payment.Amount, payment.PaymentMethod, payment.Status },
            category: AuditCategory.Financial,
            classification: DataClassification.Financial,
            ct: cancellationToken);

        // DO NOT call SaveChangesAsync here!
        // TransactionBehavior's UnitOfWork.CommitAsync will save:
        // - Payment
        // - IdempotencyRecord
        // - OutboxMessage
        // All in one transaction.

        _logger.LogInformation(
            "Payment initiated. PaymentId: {PaymentId}, InvoiceId: {InvoiceId}",
            payment.Id,
            payment.InvoiceId
        );

        var result = new InitiatePaymentResult(true, payment.Id, null);

        // Cache response for future duplicate requests
        await _idempotencyService.CacheResponseAsync(
            request.IdempotencyKey,
            result,
            statusCode: 202 // Accepted
        );

        return result;
    }
}
