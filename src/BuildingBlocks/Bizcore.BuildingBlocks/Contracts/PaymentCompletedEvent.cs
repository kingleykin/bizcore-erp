namespace Bizcore.BuildingBlocks.Contracts
{
    public interface IPaymentCompletedEvent
    {
        Guid InvoiceId { get; }
        decimal Amount { get; }
        DateTime PaymentDate { get; }
    }
}
