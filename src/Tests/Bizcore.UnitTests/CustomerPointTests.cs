using Bizcore.BuildingBlocks.Contracts;
using Customer.API.Application.Commands;
using Customer.API.Domain.Entities;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Bizcore.UnitTests
{
    public class CustomerPointTests
    {
        [Fact]
        public async Task Handle_AddCustomerPointCommand_UpdatesPointsAndPublishesEvent()
        {
            // Arrange
            using var connection = TestDbContextFactory.CreateOpenConnection();
            using var context = TestDbContextFactory.CreateCustomerDbContext(connection);

            var customer = Customers.Create("John", "Doe", "john@example.com", "0987654321", "123 Street");
            context.Customers.Add(customer);
            await context.SaveChangesAsync();

            var publishEndpointMock = new Mock<IPublishEndpoint>();
            var logger = NullLogger<AddCustomerPointCommandHandler>.Instance;

            var handler = new AddCustomerPointCommandHandler(context, publishEndpointMock.Object, logger);

            var paymentId = Guid.NewGuid();
            var command = new AddCustomerPointCommand(paymentId, customer.Id, 1500m);

            // Act
            var result = await handler.Handle(command, CancellationToken.None);
            await context.SaveChangesAsync();

            // Assert
            result.Should().BeTrue();

            var updatedCustomer = await context.Customers.FindAsync(customer.Id);
            updatedCustomer.Should().NotBeNull();
            updatedCustomer!.CustomerPoint.Should().Be(150); // 1500 / 10 = 150 points

            // Verify event was published
            publishEndpointMock.Verify(p => p.Publish<ICustomerPointAddedEvent>(It.Is<object>(obj =>
                GetPropertyValue<Guid>(obj, "PaymentId") == paymentId &&
                GetPropertyValue<Guid>(obj, "CustomerId") == customer.Id &&
                GetPropertyValue<int>(obj, "Points") == 150
            ), It.IsAny<CancellationToken>()), Times.Once);
        }

        private static T GetPropertyValue<T>(object obj, string propName)
        {
            var prop = obj.GetType().GetProperty(propName);
            if (prop == null) return default!;
            return (T)prop.GetValue(obj)!;
        }
    }
}
