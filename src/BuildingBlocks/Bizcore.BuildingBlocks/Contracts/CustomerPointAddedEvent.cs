using System;

namespace Bizcore.BuildingBlocks.Contracts
{

    /// <summary>
    /// 
    /// </summary>
    public interface ICustomerPointAddedEvent
    {
        Guid PaymentId { get; }
        Guid CustomerId { get; }
        int Points { get; }
    }


    /// <summary>
    /// Command: Saga orchestrator yêu cầu Payment service add customer points
    /// </summary>
    public interface IAddCustomerPointCommand
    {
        Guid PaymentId { get; }
        Guid CustomerId { get; }
        decimal Amount { get; }
    }
}