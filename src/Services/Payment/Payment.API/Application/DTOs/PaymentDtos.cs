namespace Payment.API.Application.DTOs;

public record PaymentResponseDto
(
    Guid PaymentId,
    Guid? InvoiceId,
    Guid? OrderId,
    decimal Amount,
    string Status,
    DateTime PaymentDate,
    string? FailureReason,
    int? ExpiresIn = null,
    int? RetryAfter = null
);

/// <summary>Đúng một trong InvoiceId/OrderId phải được cung cấp.</summary>
public record ProcessPaymentRequest
(
    Guid? InvoiceId,
    Guid? OrderId,
    decimal Amount,
    string PaymentMethod
);
