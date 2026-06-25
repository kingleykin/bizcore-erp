using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Orchestration.API.Domain.Entities;
using Bizcore.BuildingBlocks.Messaging;

namespace Orchestration.API.Application.Sagas
{
    public class PaymentSaga : MassTransitStateMachine<PaymentSagaState>
    {
        public PaymentSaga()
        {
            InstanceState(x => x.CurrentState);

            Event(() => PaymentInitiated, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => CustomerBalanceDeducted, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => CustomerBalanceDeductionFailed, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => InvoiceValidated, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => InvoiceValidationFailed, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => PaymentConfirmed, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => PaymentRejected, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => CustomerPointAdded, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => CustomerPointAdditionFailed, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => PaymentRefunded, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => InvoicePaymentReverted, x => x.CorrelateById(ctx => ctx.Message.PaymentId));
            Event(() => CustomerBalanceRefunded, x => x.CorrelateById(ctx => ctx.Message.PaymentId));

            // Timeout event for each step (30 seconds)
            Schedule(() => StepTimeout, x => x.ValidationTimeoutTokenId, s =>
            {
                s.Delay = TimeSpan.FromSeconds(30);
                s.Received = r => r.CorrelateById(ctx => ctx.Message.PaymentId);
            });

            // ── STEP 1: Payment Initiated → Trừ tiền tài khoản ──────────────────
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
                    .Schedule(StepTimeout, ctx => ctx.Init<PaymentStepTimeout>(new { PaymentId = ctx.Saga.PaymentId, CurrentState = "DeductingBalance" }))
                    .SendAsync(ctx => ctx.Init<IValidateInvoiceCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        CustomerId = ctx.Saga.CustomerId,
                        Amount = ctx.Saga.Amount,
                        InvoiceId = ctx.Saga.InvoiceId
                    }))
            );

            // ── STEP 3: Trừ tiền → Validate Invoice ─────────────────────────────
            During(DeductingBalance,
                When(CustomerBalanceDeducted)
                    .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                    .TransitionTo(Confirming)
                    .Schedule(StepTimeout, ctx => ctx.Init<PaymentStepTimeout>(new { PaymentId = ctx.Saga.PaymentId, CurrentState = "Confirming" }))
                    .SendAsync(ctx => ctx.Init<IConfirmPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        Amount = ctx.Saga.Amount
                    })),

                When(CustomerBalanceDeductionFailed)
                    .Then(ctx =>
                    {
                        ctx.Saga.FailureReason = ctx.Message.Reason;
                        ctx.Saga.UpdatedAt = DateTime.UtcNow;
                    })
                    .Unschedule(StepTimeout)
                    .SendAsync(ctx => ctx.Init<IRejectPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        Reason = ctx.Saga.FailureReason ?? "Insufficient balance"
                    }))
                    .TransitionTo(Rejected),

                When(StepTimeout.Received)
                    .Then(ctx =>
                    {
                        ctx.Saga.FailureReason = "Timeout during DeductingBalance after 30 seconds.";
                        ctx.Saga.UpdatedAt = DateTime.UtcNow;
                    })
                    .SendAsync(ctx => ctx.Init<IRejectPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        Reason = "Timeout during DeductingBalance after 30 seconds."
                    }))
                    .TransitionTo(TimedOut)
            );

            // ── STEP 2: Validating Invoice ───────────────────────────────────────
            During(Validating,
                When(InvoiceValidated)
                    .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                    .TransitionTo(DeductingBalance)
                    .Schedule(StepTimeout, ctx => ctx.Init<PaymentStepTimeout>(new { PaymentId = ctx.Saga.PaymentId, CurrentState = "DeductingBalance" }))
                    .SendAsync(ctx => ctx.Init<IDeductCustomerBalanceCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        CustomerId = ctx.Saga.CustomerId,
                        Amount = ctx.Saga.Amount
                    })),

                When(InvoiceValidationFailed)
                    .Then(ctx =>
                    {
                        ctx.Saga.FailureReason = ctx.Message.Reason;
                        ctx.Saga.UpdatedAt = DateTime.UtcNow;
                    })
                    .Unschedule(StepTimeout)
                    .SendAsync(ctx => ctx.Init<IRejectPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        Reason = ctx.Saga.FailureReason ?? "Unknown error"
                    }))
                    .TransitionTo(Rejected),

                When(StepTimeout.Received)
                    .Then(ctx =>
                    {
                        ctx.Saga.FailureReason = "Timeout during Validating Invoice after 30 seconds.";
                        ctx.Saga.UpdatedAt = DateTime.UtcNow;
                    })
                    .SendAsync(ctx => ctx.Init<IRejectPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        Reason = "Timeout during Validating Invoice after 30 seconds."
                    }))
                    .TransitionTo(TimedOut)
            );

            // ── STEP 4: Confirming → UpdatingPoints ─────────────────────────────
            During(Confirming,
                When(PaymentConfirmed)
                    .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                    .TransitionTo(UpdatingPoints)
                    .Schedule(StepTimeout, ctx => ctx.Init<PaymentStepTimeout>(new { PaymentId = ctx.Saga.PaymentId, CurrentState = "UpdatingPoints" }))
                    .SendAsync(ctx => ctx.Init<IAddCustomerPointCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        CustomerId = ctx.Saga.CustomerId,
                        Amount = ctx.Saga.Amount
                    })),

                When(StepTimeout.Received)
                    .Then(ctx =>
                    {
                        ctx.Saga.FailureReason = "Timeout during Confirming Payment after 30 seconds.";
                        ctx.Saga.UpdatedAt = DateTime.UtcNow;
                    })
                    // Initiate compensation fallback
                    .SendAsync(ctx => ctx.Init<IRefundPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        Reason = "Timeout during Confirming Payment after 30 seconds."
                    }))
                    .TransitionTo(Compensating)
            );

            // ── STEP 5: UpdatingPoints → Completed or Compensating ──────────────
            During(UpdatingPoints,
                When(CustomerPointAdded)
                    .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                    .Unschedule(StepTimeout)
                    .Finalize(),

                When(CustomerPointAdditionFailed)
                    .Then(ctx =>
                    {
                        ctx.Saga.UpdatedAt = DateTime.UtcNow;
                        ctx.Saga.FailureReason = ctx.Message.Reason;
                    })
                    .Unschedule(StepTimeout)
                    .SendAsync(ctx => ctx.Init<IRefundPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        Reason = ctx.Saga.FailureReason ?? "Unknown error"
                    }))
                    .TransitionTo(Compensating),

                When(StepTimeout.Received)
                    .Then(ctx =>
                    {
                        ctx.Saga.FailureReason = "Timeout during Updating Points after 30 seconds.";
                        ctx.Saga.UpdatedAt = DateTime.UtcNow;
                    })
                    // Initiate compensation fallback
                    .SendAsync(ctx => ctx.Init<IRefundPaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        Reason = "Timeout during Updating Points after 30 seconds."
                    }))
                    .TransitionTo(Compensating)
            );

            // ── FINAL STATES: Rejected / TimedOut ──────────────────────────────
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

            // ── COMPENSATION: Hoàn tiền Payment → Hoàn tiền tài khoản ──────────
            During(Compensating,
                When(PaymentRefunded)
                    .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                    .SendAsync(ctx => ctx.Init<IRefundCustomerBalanceCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        CustomerId = ctx.Saga.CustomerId,
                        Amount = ctx.Saga.Amount,
                        Reason = ctx.Saga.FailureReason ?? "Compensation rollback"
                    }))
                    .TransitionTo(RefundingBalance)
            );

            // ── COMPENSATION: Hoàn tiền tài khoản → Revert Invoice ─────────────
            During(RefundingBalance,
                When(CustomerBalanceRefunded)
                    .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                    .SendAsync(ctx => ctx.Init<IRevertInvoicePaymentCommand>(new
                    {
                        PaymentId = ctx.Saga.PaymentId,
                        InvoiceId = ctx.Saga.InvoiceId,
                        Reason = ctx.Saga.FailureReason ?? "Unknown error"
                    }))
                    .TransitionTo(Reverting)
            );

            // ── COMPENSATION: Revert Invoice → Finalize ─────────────────────────
            During(Reverting,
                When(InvoicePaymentReverted)
                    .Then(ctx => ctx.Saga.UpdatedAt = DateTime.UtcNow)
                    .Finalize()
            );

            SetCompletedWhenFinalized();
        }

        public State DeductingBalance { get; private set; } = null!;
        public State Validating { get; private set; } = null!;
        public State Confirmed { get; private set; } = null!;
        public State Rejected { get; private set; } = null!;
        public State TimedOut { get; private set; } = null!;
        public State Confirming { get; private set; } = null!;
        public State UpdatingPoints { get; private set; } = null!;
        public State Compensating { get; private set; } = null!;
        public State RefundingBalance { get; private set; } = null!;
        public State Reverting { get; private set; } = null!;

        public Event<IPaymentInitiatedEvent> PaymentInitiated { get; private set; } = null!;
        public Event<ICustomerBalanceDeductedEvent> CustomerBalanceDeducted { get; private set; } = null!;
        public Event<ICustomerBalanceDeductionFailedEvent> CustomerBalanceDeductionFailed { get; private set; } = null!;
        public Event<IInvoiceValidatedEvent> InvoiceValidated { get; private set; } = null!;
        public Event<IInvoiceValidationFailedEvent> InvoiceValidationFailed { get; private set; } = null!;
        public Event<IPaymentConfirmedEvent> PaymentConfirmed { get; private set; } = null!;
        public Event<IPaymentRejectedEvent> PaymentRejected { get; private set; } = null!;
        public Event<ICustomerPointAddedEvent> CustomerPointAdded { get; private set; } = null!;
        public Event<ICustomerPointAdditionFailedEvent> CustomerPointAdditionFailed { get; private set; } = null!;
        public Event<IPaymentRefundedEvent> PaymentRefunded { get; private set; } = null!;
        public Event<ICustomerBalanceRefundedEvent> CustomerBalanceRefunded { get; private set; } = null!;
        public Event<IInvoicePaymentRevertedEvent> InvoicePaymentReverted { get; private set; } = null!;

        public Schedule<PaymentSagaState, PaymentStepTimeout> StepTimeout { get; private set; } = null!;
    }

    public record PaymentStepTimeout
    {
        public Guid PaymentId { get; init; }
        public string CurrentState { get; init; } = string.Empty;
    }
}
