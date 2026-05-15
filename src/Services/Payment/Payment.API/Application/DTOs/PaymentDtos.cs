namespace Payment.API.Application.DTOs;

public record PaymentResponseDto
(
    Guid PaymentId,
    Guid InvoiceId,
    decimal Amount,
    string Status,
    DateTime PaymentDate,
    string? FailureReason,
    int? ExpiresIn = null,
    int? RetryAfter = null
);

public record ProcessPaymentRequest
(
    Guid InvoiceId,
    decimal Amount,
    string PaymentMethod
);
