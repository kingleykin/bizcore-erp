namespace Bizcore.BuildingBlocks.Contracts
{
    public interface IPaymentCompletedEvent
    {
        Guid PaymentId { get; }
        Guid? InvoiceId { get; }
        Guid? OrderId { get; }
        decimal Amount { get; }
        DateTime PaymentDate { get; }
    }
}
