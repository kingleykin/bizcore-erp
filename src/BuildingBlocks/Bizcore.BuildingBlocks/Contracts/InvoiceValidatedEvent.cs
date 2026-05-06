namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event: Invoice service đã validate thành công và đánh dấu invoice là Paid.
    /// Saga orchestrator sẽ nhận event này để confirm payment.
    /// </summary>
    public interface IInvoiceValidatedEvent
    {
        Guid PaymentId { get; }
        Guid InvoiceId { get; }
        DateTime ValidatedAt { get; }
    }

    /// <summary>
    /// Event: Invoice service từ chối validation (invoice không tồn tại, đã paid, amount mismatch, v.v.).
    /// Saga orchestrator sẽ nhận event này để reject payment và trigger compensation.
    /// </summary>
    public interface IInvoiceValidationFailedEvent
    {
        Guid PaymentId { get; }
        Guid InvoiceId { get; }
        string Reason { get; }
        DateTime FailedAt { get; }
    }
}
