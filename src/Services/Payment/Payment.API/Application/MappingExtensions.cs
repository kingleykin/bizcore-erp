using PaymentEntity = Payment.API.Domain.Entities.Payment;
using Payment.API.Application.DTOs;
using Payment.API.Domain.Entities;

namespace Payment.API.Application;

public static class MappingExtensions
{
    public static PaymentResponseDto ToDto(this PaymentEntity entity)
    {
        var response = new PaymentResponseDto(
            PaymentId: entity.Id,
            InvoiceId: entity.InvoiceId,
            Amount: entity.Amount,
            Status: entity.Status.ToString(),
            PaymentDate: entity.PaymentDate,
            FailureReason: entity.FailureReason
        );

        if (entity.Status == PaymentStatus.Processing)
        {
            var elapsed = (DateTime.UtcNow - entity.PaymentDate).TotalSeconds;
            var timeout = 60; // Saga timeout
            var expiresIn = Math.Max(0, (int)(timeout - elapsed));

            var retryAfter = elapsed switch
            {
                < 10 => 2,
                < 30 => 5,
                _ => 10
            };

            return response with { ExpiresIn = expiresIn, RetryAfter = retryAfter };
        }

        return response;
    }
}
