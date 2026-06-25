using System;

namespace Bizcore.BuildingBlocks.Contracts
{
    /// <summary>
    /// Command: Saga gửi tới Customer.API để trừ tiền tài khoản trước khi thanh toán
    /// </summary>
    public interface IDeductCustomerBalanceCommand
    {
        Guid PaymentId { get; }
        Guid CustomerId { get; }
        decimal Amount { get; }
    }

    /// <summary>
    /// Event: Trừ tiền tài khoản thành công
    /// </summary>
    public interface ICustomerBalanceDeductedEvent
    {
        Guid PaymentId { get; }
        Guid CustomerId { get; }
        decimal AmountDeducted { get; }
    }

    /// <summary>
    /// Event: Trừ tiền tài khoản thất bại (không đủ tiền)
    /// </summary>
    public interface ICustomerBalanceDeductionFailedEvent
    {
        Guid PaymentId { get; }
        Guid CustomerId { get; }
        string Reason { get; }
    }

    /// <summary>
    /// Command: Saga gửi tới Customer.API để hoàn tiền khi rollback
    /// </summary>
    public interface IRefundCustomerBalanceCommand
    {
        Guid PaymentId { get; }
        Guid CustomerId { get; }
        decimal Amount { get; }
        string Reason { get; }
    }

    /// <summary>
    /// Event: Hoàn tiền tài khoản thành công
    /// </summary>
    public interface ICustomerBalanceRefundedEvent
    {
        Guid PaymentId { get; }
        Guid CustomerId { get; }
        decimal AmountRefunded { get; }
    }
}
