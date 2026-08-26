using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Orchestration.API.Domain.Entities;
using Bizcore.BuildingBlocks.Messaging;

namespace Orchestration.API.Application.Sagas
{
    /// <summary>
    /// Saga orchestrator cho payment flow — dùng chung cho cả thanh toán Hóa đơn (Invoice) lẫn
    /// Đơn hàng (Order). Đúng một trong InvoiceId/OrderId được set trên IPaymentInitiatedEvent,
    /// saga rẽ nhánh gửi lệnh validate tới đúng service tương ứng; phần Confirm/Reject dùng
    /// chung logic vì Payment.API chỉ cần PaymentId để xử lý.
    ///
    /// Không tạo saga riêng cho Order: nếu có 2 saga cùng lắng nghe IPaymentInitiatedEvent thì cả
    /// 2 sẽ cùng khởi tạo instance cho MỌI payment (kể cả loại không thuộc về mình) — MassTransit
    /// correlate theo PaymentId trên từng saga type độc lập, không biết "loại" payment để lọc.
    ///
    /// Flow:
    /// 1. Nhận IPaymentInitiatedEvent từ Payment service
    /// 2a. Nếu OrderId có giá trị: gửi IValidateOrderCommand đến Order service
    /// 2b. Ngược lại (InvoiceId): gửi IValidateInvoiceCommand đến Invoice service
    /// 3a. Nhận Validated (Invoice hoặc Order) → gửi IConfirmPaymentCommand → Confirmed
    /// 3b. Nhận ValidationFailed → gửi IRejectPaymentCommand → Rejected
    /// 4. Timeout sau 60 giây nếu không nhận được response → auto reject
    ///
    /// States: Initiated → Validating → Confirmed / Rejected / TimedOut
    /// </summary>
    public class PaymentSaga : MassTransitStateMachine<PaymentSagaState>
    {
        public PaymentSaga()
        {
            // Định nghĩa property nào là CurrentState
            InstanceState(x => x.CurrentState);

            // Định nghĩa events
            Event(() => PaymentInitiated, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => InvoiceValidated, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => InvoiceValidationFailed, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => OrderValidated, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => OrderValidationFailed, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => PaymentConfirmed, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => PaymentRejected, x => x.CorrelateById(ctx => ctx.Message.PaymentId));

            // Timeout event
            Schedule(() => ValidationTimeout, x => x.ValidationTimeoutTokenId, s =>
            {
                s.Delay = TimeSpan.FromSeconds(60);
                s.Received = r => r.CorrelateById(ctx => ctx.Message.PaymentId);
            });

            // Định nghĩa state machine
            Initially(
                // Payment cho Đơn hàng (OrderId có giá trị) → validate qua Order service
                When(PaymentInitiated, ctx => ctx.Message.OrderId.HasValue)
                    .Then(InitializeSagaFromPaymentInitiated)
                    .TransitionTo(Validating)
                    .Schedule(ValidationTimeout, ctx => ctx.Init<PaymentValidationTimeout>(new
                    {
                        PaymentId = ctx.Saga.PaymentId
                    }))
                    .SendAsync(ctx => ctx.Init<IValidateOrderCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        OrderId = ctx.Saga.OrderId!.Value,
                        Amount = ctx.Saga.Amount
                    })),

                // Payment cho Hóa đơn (InvoiceId có giá trị) → validate qua Invoice service
                When(PaymentInitiated, ctx => !ctx.Message.OrderId.HasValue)
                    .Then(InitializeSagaFromPaymentInitiated)
                    .TransitionTo(Validating)
                    .Schedule(ValidationTimeout, ctx => ctx.Init<PaymentValidationTimeout>(new
                    {
                        PaymentId = ctx.Saga.PaymentId
                    }))
                    .SendAsync(ctx => ctx.Init<IValidateInvoiceCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId!.Value,
                        Amount = ctx.Saga.Amount
                    }))
            );

            During(Validating,
                // Happy path (Invoice): Invoice validated → confirm payment
                When(InvoiceValidated)
                    .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                    .Unschedule(ValidationTimeout)
                    .SendAsync(ctx => ctx.Init<IConfirmPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        OrderId = ctx.Saga.OrderId
                    }))
                    .TransitionTo(Confirmed),

                // Failure path (Invoice): validation failed → reject payment
                When(InvoiceValidationFailed)
                    .Then(ctx =>
                    {
                        ctx.Saga.FailureReason = ctx.Message.Reason;
                        ctx.Saga.UpdatedAt = DateTime.UtcNow;
                    })
                    .Unschedule(ValidationTimeout)
                    .SendAsync(ctx => ctx.Init<IRejectPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        OrderId = ctx.Saga.OrderId,
                        Reason = ctx.Saga.FailureReason ?? "Unknown error"
                    }))
                    .TransitionTo(Rejected),

                // Happy path (Order): Order validated → confirm payment
                When(OrderValidated)
                    .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                    .Unschedule(ValidationTimeout)
                    .SendAsync(ctx => ctx.Init<IConfirmPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        OrderId = ctx.Saga.OrderId
                    }))
                    .TransitionTo(Confirmed),

                // Failure path (Order): validation failed → reject payment
                When(OrderValidationFailed)
                    .Then(ctx =>
                    {
                        ctx.Saga.FailureReason = ctx.Message.Reason;
                        ctx.Saga.UpdatedAt = DateTime.UtcNow;
                    })
                    .Unschedule(ValidationTimeout)
                    .SendAsync(ctx => ctx.Init<IRejectPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        OrderId = ctx.Saga.OrderId,
                        Reason = ctx.Saga.FailureReason ?? "Unknown error"
                    }))
                    .TransitionTo(Rejected),

                // Timeout path: không nhận được response sau 60 giây (áp dụng cho cả 2 luồng)
                When(ValidationTimeout.Received)
                    .Then(ctx =>
                    {
                        ctx.Saga.FailureReason = "Validation timeout after 60 seconds.";
                        ctx.Saga.UpdatedAt = DateTime.UtcNow;
                    })
                    .SendAsync(ctx => ctx.Init<IRejectPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        OrderId = ctx.Saga.OrderId,
                        Reason = "Validation timeout after 60 seconds."
                    }))
                    .TransitionTo(TimedOut)
            );

            During(Confirmed,
                When(PaymentConfirmed)
                    .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                    .Finalize()
            );

            During(Rejected,
                When(PaymentRejected)
                    .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                    .Finalize()
            );

            During(TimedOut,
                When(PaymentRejected)
                    .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                    .Finalize()
            );

            SetCompletedWhenFinalized();
        }

        private static void InitializeSagaFromPaymentInitiated(BehaviorContext<PaymentSagaState, IPaymentInitiatedEvent> ctx)
        {
            ctx.Saga.CorrelationId = ctx.Message.PaymentId;
            ctx.Saga.PaymentId = ctx.Message.PaymentId;
            ctx.Saga.InvoiceId = ctx.Message.InvoiceId;
            ctx.Saga.OrderId = ctx.Message.OrderId;
            ctx.Saga.Amount = ctx.Message.Amount;
            ctx.Saga.IdempotencyKey = ctx.Message.IdempotencyKey;
            ctx.Saga.CreatedAt = ctx.Message.InitiatedAt;
            ctx.Saga.UpdatedAt = DateTime.UtcNow;
        }

        // States
        public State Validating { get; private set; } = null!;
        public State Confirmed { get; private set; } = null!;
        public State Rejected { get; private set; } = null!;
        public State TimedOut { get; private set; } = null!;

        // Events
        public Event<IPaymentInitiatedEvent> PaymentInitiated { get; private set; } = null!;
        public Event<IInvoiceValidatedEvent> InvoiceValidated { get; private set; } = null!;
        public Event<IInvoiceValidationFailedEvent> InvoiceValidationFailed { get; private set; } = null!;
        public Event<IOrderValidatedEvent> OrderValidated { get; private set; } = null!;
        public Event<IOrderValidationFailedEvent> OrderValidationFailed { get; private set; } = null!;
        public Event<IPaymentConfirmedEvent> PaymentConfirmed { get; private set; } = null!;
        public Event<IPaymentRejectedEvent> PaymentRejected { get; private set; } = null!;

        // Timeout schedule
        public Schedule<PaymentSagaState, PaymentValidationTimeout> ValidationTimeout { get; private set; } = null!;
    }

    /// <summary>
    /// Timeout message cho saga validation.
    /// </summary>
    public record PaymentValidationTimeout
    {
        public Guid PaymentId { get; init; }
    }
}
