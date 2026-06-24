using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;

namespace Customer.API.Domain.Entities
{
    public class Customers : BaseEntity
    {
        public string FirstName { get; private set; }
        public string LastName { get; private set; }
        public string Email { get; private set; }
        public string Phone { get; private set; }
        public string Address { get; private set; }
        public int CustomerPoint { get; private set; } = 0;
        public CustomerStatus Status { get; private set; } = CustomerStatus.CreatedUser;
        public int CustomerGroupId { get; private set; }
        public CustomerGroup? CustomerGroup { get; private set; } = null;

        /// <summary>
        /// Create new customer
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="email"></param>
        /// <param name="phone"></param>
        /// <param name="address"></param>

        /// <returns></returns>
        public static Customers Create(string firstName, string lastName, string email, string phone, string address)
        {
            if (string.IsNullOrWhiteSpace(firstName))
            {
                throw new Exception("First name is required.");
            }
            if (string.IsNullOrWhiteSpace(lastName))
            {
                throw new Exception("Last name is required.");
            }
            if (string.IsNullOrWhiteSpace(email))
            {
                throw new Exception("Email is required.");
            }
            if (!email.Contains("@"))
            {
                throw new Exception("Email is invalid.");
            }
            if (string.IsNullOrEmpty(phone) || phone.Length < 9 || phone.Length > 15)
            {
                throw new Exception("Phone is invalid.");
            }
            return new Customers
            {
                FirstName = firstName,
                LastName = lastName,
                Email = email,
                Phone = phone,
                Address = address,
                CustomerPoint = 0

            };
        }


    }


}
