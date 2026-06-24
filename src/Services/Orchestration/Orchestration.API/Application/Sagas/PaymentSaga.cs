using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Orchestration.API.Domain.Entities;
using Bizcore.BuildingBlocks.Messaging;

namespace Orchestration.API.Application.Sagas
{
    /// <summary>
    /// Saga orchestrator cho payment flow.
    /// 
    /// Flow:
    /// 1. Nhận IPaymentInitiatedEvent từ Payment service
    /// 2. Gửi IValidateInvoiceCommand đến Invoice service
    /// 3a. Nhận IInvoiceValidatedEvent → gửi IConfirmPaymentCommand → Completed
    /// 3b. Nhận IInvoiceValidationFailedEvent → gửi IRejectPaymentCommand → Rejected
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
            Event(() => PaymentConfirmed, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => PaymentRejected, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => CustomerPointAdded, x => x.CorrelateById(ctx => ctx.Message.PaymentId));

            // Timeout event
            Schedule(() => ValidationTimeout, x => x.ValidationTimeoutTokenId, s =>
            {
                s.Delay = TimeSpan.FromSeconds(60);
                s.Received = r => r.CorrelateById(ctx => ctx.Message.PaymentId);
            });

            // Định nghĩa state machine
            Initially(
                When(PaymentInitiated)
                    .Then(ctx =>
                    {
                        ctx.Saga.CorrelationId = ctx.Message.PaymentId;
                        ctx.Saga.PaymentId = ctx.Message.PaymentId;
                        ctx.Saga.InvoiceId = ctx.Message.InvoiceId;
                        ctx.Saga.Amount = ctx.Message.Amount;
                        ctx.Saga.CustomerId = ctx.Message.CustomerId;
                        ctx.Saga.IdempotencyKey = ctx.Message.IdempotencyKey;
                        ctx.Saga.CreatedAt = ctx.Message.InitiatedAt;
                        ctx.Saga.UpdatedAt = DateTime.UtcNow;
                    })
                    .TransitionTo(Validating)
                    // Schedule timeout
                    .Schedule(ValidationTimeout, ctx => ctx.Init<PaymentValidationTimeout>(new
                    {
                        PaymentId = ctx.Saga.PaymentId
                    }))
                    .SendAsync(ctx => ctx.Init<IValidateInvoiceCommand>(new
                    {
                        CustomerId = ctx.Saga.CustomerId,
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        Amount = ctx.Saga.Amount
                    }))
            );

            During(Validating,
                // Happy path: Invoice validated → confirm payment
                When(InvoiceValidated)
                    .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                    .Unschedule(ValidationTimeout)
                    .SendAsync(ctx => ctx.Init<IConfirmPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId
                    }))
                    .TransitionTo(Confirmed),

                // Failure path: Invoice validation failed → reject payment
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
                        Reason = ctx.Saga.FailureReason ?? "Unknown error"
                    }))
                    .TransitionTo(Rejected),

                // Timeout path: không nhận được response sau 60 giây
                When(ValidationTimeout.Received)
                    .Then(ctx =>
                    {
                        ctx.Saga.FailureReason = "Invoice validation timeout after 60 seconds.";
                        ctx.Saga.UpdatedAt = DateTime.UtcNow;
                    })
                    .SendAsync(ctx => ctx.Init<IRejectPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        Reason = "Invoice validation timeout after 60 seconds."
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

        // States
        public State Validating { get; private set; } = null!;
        public State Confirmed { get; private set; } = null!;
        public State Rejected { get; private set; } = null!;
        public State TimedOut { get; private set; } = null!;
        public State Confirming { get; private set; } = null!;
        public State UpdatingPoints { get; private set; } = null!;

        // Events
        public Event<IPaymentInitiatedEvent> PaymentInitiated { get; private set; } = null!;
        public Event<IInvoiceValidatedEvent> InvoiceValidated { get; private set; } = null!;
        public Event<IInvoiceValidationFailedEvent> InvoiceValidationFailed { get; private set; } = null!;
        public Event<IPaymentConfirmedEvent> PaymentConfirmed { get; private set; } = null!;
        public Event<IPaymentRejectedEvent> PaymentRejected { get; private set; } = null!;
        public Event<ICustomerPointAddedEvent> CustomerPointAdded { get; private set; } = null!;

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
