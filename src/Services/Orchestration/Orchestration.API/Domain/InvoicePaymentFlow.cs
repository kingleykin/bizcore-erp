namespace Orchestration.API.Domain;

public static class InvoicePaymentFlow
{
    public const string FlowTypeConstant = "invoice-payment";

    public static class States
    {
        public const string InvoiceIndexed = nameof(InvoiceIndexed);
        public const string PaymentInitiated = nameof(PaymentInitiated);
        public const string DeductingBalance = nameof(DeductingBalance);
        public const string BalanceDeducted = nameof(BalanceDeducted);
        public const string PaymentCaptured = nameof(PaymentCaptured);
        public const string CompensationRequired = nameof(CompensationRequired);
        public const string PaymentConfirmed = nameof(PaymentConfirmed);
        public const string CustomerPointAdded = nameof(CustomerPointAdded);
        public const string Compensating = nameof(Compensating);
        public const string RefundingBalance = nameof(RefundingBalance);
        public const string Reverting = nameof(Reverting);
        public const string Refunded = nameof(Refunded);
        public const string Failed = nameof(Failed);
        public const string InsufficientBalance = nameof(InsufficientBalance);
    }

    public static class Steps
    {
        public const string InvoiceCreatedObserved = nameof(InvoiceCreatedObserved);
        public const string PaymentInitiatedObserved = nameof(PaymentInitiatedObserved);
        public const string BalanceDeductedObserved = nameof(BalanceDeductedObserved);
        public const string BalanceDeductionFailedObserved = nameof(BalanceDeductionFailedObserved);
        public const string PaymentCompletedObserved = nameof(PaymentCompletedObserved);
        public const string CustomerPointAddedObserved = nameof(CustomerPointAddedObserved);
        public const string PaymentCompensationRequestedObserved = nameof(PaymentCompensationRequestedObserved);
        public const string CustomerPointAdditionFailedObserved = nameof(CustomerPointAdditionFailedObserved);
        public const string PaymentRefundedObserved = nameof(PaymentRefundedObserved);
        public const string CustomerBalanceRefundedObserved = nameof(CustomerBalanceRefundedObserved);
        public const string InvoicePaymentRevertedObserved = nameof(InvoicePaymentRevertedObserved);
    }
}
