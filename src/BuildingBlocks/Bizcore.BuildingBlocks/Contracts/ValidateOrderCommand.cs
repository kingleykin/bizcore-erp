namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Command: Saga orchestrator yêu cầu Order service validate đơn hàng trước khi
    /// confirm payment. Async command, không phải Request-Reply — cùng mô hình với
    /// IValidateInvoiceCommand.
    /// </summary>
    public interface IValidateOrderCommand
    {
        Guid PaymentId { get; }
        Guid OrderId { get; }
        decimal Amount { get; }
    }
}
