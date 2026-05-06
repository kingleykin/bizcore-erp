namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Request-Reply pattern: Payment service yêu cầu Invoice service
    /// xác nhận có thể áp dụng payment hay không TRƯỚC KHI commit.
    /// </summary>
    public interface IApplyPaymentToInvoiceRequest
    {
        Guid PaymentId { get; }
        Guid InvoiceId { get; }
        decimal Amount { get; }
    }

    public interface IApplyPaymentToInvoiceResponse
    {
        bool Success { get; }
        string? ErrorReason { get; }
    }
}
