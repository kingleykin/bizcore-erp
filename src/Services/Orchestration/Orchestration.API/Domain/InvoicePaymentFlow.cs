namespace Orchestration.API.Domain;

public static class InvoicePaymentFlow
{
    public const string FlowTypeConstant = "invoice-payment";

    public static class States
    {
        public const string InvoiceIndexed = nameof(InvoiceIndexed);
        public const string PaymentCaptured = nameof(PaymentCaptured);
        public const string CompensationRequired = nameof(CompensationRequired);
    }

    public static class Steps
    {
        public const string InvoiceCreatedObserved = nameof(InvoiceCreatedObserved);
        public const string PaymentCompletedObserved = nameof(PaymentCompletedObserved);
        public const string PaymentCompensationRequestedObserved = nameof(PaymentCompensationRequestedObserved);
    }
}
