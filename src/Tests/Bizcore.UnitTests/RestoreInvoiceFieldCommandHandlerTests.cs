using System.Security.Claims;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using Invoice.API.Application.Commands;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bizcore.UnitTests;

public class RestoreInvoiceFieldCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidRequest_PublishesAuditEvent()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(dbName);
        
        var invoice = Invoice.API.Domain.Entities.Invoice.Create("Old Name", 1000);
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var publishEndpointMock = new Mock<IPublishEndpoint>();
        var loggerMock = new Mock<ILogger<RestoreInvoiceFieldCommandHandler>>();
        
        var handler = new RestoreInvoiceFieldCommandHandler(context, publishEndpointMock.Object, loggerMock.Object);

        var actor = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", "user-123"),
            new Claim(ClaimTypes.Name, "test-user")
        }));

        var command = new RestoreInvoiceFieldCommand(
            InvoiceId: invoice.Id,
            Field: "CustomerName",
            PreviousValue: "Restored Name",
            SourceAuditEntryId: Guid.NewGuid(),
            Reason: "Manual correction",
            Actor: actor
        );

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        
        // Verify AuditEvent was published
        publishEndpointMock.Verify(p => p.Publish(
            It.Is<AuditEvent>(e => 
                e.ServiceName == "Invoice.API" &&
                e.Action == "DataReversal.Invoice.CustomerName" &&
                e.EntityType == "Invoice" &&
                e.EntityId == invoice.Id.ToString() &&
                e.ActorUserId == "user-123"
            ), 
            It.IsAny<CancellationToken>()
        ), Times.Once);

        // Verify entity was updated
        var updatedInvoice = await context.Invoices.FindAsync(invoice.Id);
        updatedInvoice!.CustomerName.Should().Be("Restored Name");
    }
}
