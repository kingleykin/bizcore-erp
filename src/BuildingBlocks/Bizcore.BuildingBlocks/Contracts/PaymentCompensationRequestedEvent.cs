namespace Bizcore.BuildingBlocks.Contracts
{
    public interface IPaymentCompensationRequestedEvent
    {
        Guid PaymentId { get; }
        Guid InvoiceId { get; }
        decimal Amount { get; }
        DateTime RequestedAt { get; }
        string Reason { get; }
    }
}
