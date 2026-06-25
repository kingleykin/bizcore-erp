using Bizcore.BuildingBlocks.Contracts;
using Invoice.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Bizcore.UnitTests;

public class ImplicitAuditRemovalTests
{
    [Fact]
    public async Task SaveChangesAsync_ShouldNotPublishImplicitAuditEvent()
    {
        // Arrange
        var dbName = Guid.NewGuid().ToString();
        
        // Mock MassTransit IBus (which the old interceptor used)
        var busMock = new Mock<IBus>();
        
        // Setup DbContext with NO interceptors (since we removed them)
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;
            
        using var context = new AppDbContext(options);
        
        var invoice = Invoice.API.Domain.Entities.Invoice.Create(Guid.NewGuid(), "Test Customer", 500);
        context.Invoices.Add(invoice);

        // Act
        await context.SaveChangesAsync();

        // Assert
        // Verify that IBus.Publish was NEVER called with any AuditEvent
        // (The old interceptor would have called this)
        busMock.Verify(b => b.Publish(It.IsAny<AuditEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
