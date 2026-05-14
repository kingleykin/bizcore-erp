using Bizcore.BuildingBlocks.Abstractions;
using MassTransit;

namespace Orchestration.API.Domain.Entities
{
    /// <summary>
    /// Saga state entity cho payment flow orchestration.
    /// MassTransit sẽ persist state này vào database để đảm bảo saga có thể recover sau crash.
    /// </summary>
    public class PaymentSagaState : BaseEntity, SagaStateMachineInstance
    {
        /// <summary>CorrelationId — MassTransit dùng để track saga instance.</summary>
        public Guid CorrelationId { get; set; }

        /// <summary>Current state của saga (Initiated, Validating, Confirmed, Rejected, Failed).</summary>
        public string CurrentState { get; set; } = string.Empty;

        /// <summary>PaymentId được tạo bởi Payment service.</summary>
        public Guid PaymentId { get; set; }

        /// <summary>InvoiceId cần validate.</summary>
        public Guid InvoiceId { get; set; }

        /// <summary>Amount cần validate với invoice.</summary>
        public decimal Amount { get; set; }

        /// <summary>Idempotency key từ client request.</summary>
        public string IdempotencyKey { get; set; } = string.Empty;

        /// <summary>Lý do reject nếu validation failed.</summary>
        public string? FailureReason { get; set; }

        /// <summary>Token ID cho timeout schedule.</summary>
        public Guid? ValidationTimeoutTokenId { get; set; }
    }
}
