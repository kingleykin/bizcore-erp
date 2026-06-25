using System;

namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Event thông báo cộng điểm thất bại
    /// </summary>
    public interface ICustomerPointAdditionFailedEvent
    {
        Guid PaymentId { get; }
        Guid CustomerId { get; }
        string Reason { get; }
    }

    /// <summary>
    /// Command gửi tới Payment.API để hoàn tiền/hủy thanh toán
    /// </summary>
    public interface IRefundPaymentCommand
    {
        Guid PaymentId { get; }
        Guid InvoiceId { get; }
        string Reason { get; }
    }

    /// <summary>
    /// Command gửi tới Invoice.API để trả trạng thái hóa đơn về Pending
    /// </summary>
    public interface IRevertInvoicePaymentCommand
    {
        Guid PaymentId { get; }
        Guid InvoiceId { get; }
        string Reason { get; }
    }

    /// <summary>
    /// Event báo đã hoàn tiền xong
    /// </summary>
    public interface IPaymentRefundedEvent
    {
        Guid PaymentId { get; }
        string Reason { get; }
    }

    /// <summary>
    /// Event báo đã rollback trạng thái hóa đơn xong
    /// </summary>
    public interface IInvoicePaymentRevertedEvent
    {
        Guid PaymentId { get; }
        Guid InvoiceId { get; }
        string Reason { get; }
    }
}
