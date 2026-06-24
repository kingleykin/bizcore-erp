using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;


namespace Customer.API.Domain.Entities
{

    public class CustomerGroup : BaseEntity
    {
        public string NameCustomerGroup { get; private set; }
        public string Code { get; private set; }
        public string Description { get; private set; }
        public CustomerGroupStatus Status { get; private set; } = CustomerGroupStatus.Active;

        public static CustomerGroup Create(string name, string code, string description)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new Exception("Name is required.");
            }
            if (string.IsNullOrEmpty(code))
            {
                throw new Exception("Code is required.");
            }
            return new CustomerGroup
            {
                NameCustomerGroup = name,
                Code = code,
                Description = description
            };
        }

        public void UpdateStatus(CustomerGroupStatus status)
        {
            Status = status;
        }

        public void UpdateNameCustomerGroup(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new Exception("Name is required.");
            }
            NameCustomerGroup = name;
        }

        public void UpdateDescription(string description)
        {
            Description = description;
        }
    }
}