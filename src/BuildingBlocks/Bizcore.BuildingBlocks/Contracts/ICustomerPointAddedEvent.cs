using System;

namespace Bizcore.BuildingBlocks.Contracts
{
    public interface ICustomerPointAddedEvent
    {
        Guid PaymentId { get; }
        Guid CustomerId { get; }
        int Points { get; }
    }
}