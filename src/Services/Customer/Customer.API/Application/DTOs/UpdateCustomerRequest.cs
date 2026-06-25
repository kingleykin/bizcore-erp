namespace Customer.API.Application.DTOs
{
    public class UpdateCustomerRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public Guid? CustomerGroupId { get; set; } = null;
        public long Version { get; set; }
    }
}
