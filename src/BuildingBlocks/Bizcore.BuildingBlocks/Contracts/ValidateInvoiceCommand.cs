namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Command: Saga orchestrator yêu cầu Invoice service validate invoice
    /// trước khi confirm payment. Đây là async command, không phải Request-Reply.
    /// </summary>
    public interface IValidateInvoiceCommand
    {
        Guid PaymentId { get; }
        Guid InvoiceId { get; }
        decimal Amount { get; }
    }
}
