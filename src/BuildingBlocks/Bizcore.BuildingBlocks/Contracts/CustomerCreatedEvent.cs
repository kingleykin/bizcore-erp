using System;

namespace Bizcore.BuildingBlocks.Contracts
{
    public interface ICustomerCreatedEvent
    {
        Guid Id { get; }
        string FirstName { get; }
        string LastName { get; }
        string Email { get; }
        string Phone { get; }
        string Address { get; }
        int Status { get; }
        DateTime CreatedAt { get; }
    }
}
