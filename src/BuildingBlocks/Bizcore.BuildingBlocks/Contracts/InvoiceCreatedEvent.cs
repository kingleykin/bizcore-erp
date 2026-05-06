using System;

namespace Bizcore.BuildingBlocks.Contracts
{
    public interface IInvoiceCreatedEvent
    {
        Guid Id { get; }
        string CustomerName { get; }
        decimal Amount { get; }
        DateTime CreatedAt { get; }
    }
}
