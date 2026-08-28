using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Exceptions;
using FluentAssertions;
using Invoice.API.Application.Consumers;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using InvoiceEntity = Invoice.API.Domain.Entities.Invoice;

namespace Bizcore.UnitTests;

/// <summary>
/// Invoice giờ là chứng từ phái sinh từ Order — sinh tự động ngay sau khi Order Confirm, không
/// còn tạo thủ công/thanh toán độc lập. Test cả domain factory và consumer sinh Invoice.
/// </summary>
public class InvoiceOrderDerivationTests
{
    private static Mock<ConsumeContext<TMessage>> BuildConsumeContext<TMessage>(TMessage message)
        where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    // ---------- Domain: Invoice.CreateFromOrder ----------

    [Fact]
    public void CreateFromOrder_WithValidData_SetsOrderId_AndStatusPaidDirectly()
    {
        var orderId = Guid.NewGuid();
        var invoice = InvoiceEntity.CreateFromOrder(orderId, "Khách A", 500m);

        invoice.OrderId.Should().Be(orderId);
        invoice.CustomerName.Should().Be("Khách A");
        invoice.Amount.Should().Be(500m);
        invoice.Status.Should().Be(Bizcore.BuildingBlocks.InvoiceStatus.Paid,
            "Invoice chỉ được sinh SAU khi Order đã thanh toán — không còn trạng thái Pending trung gian");
    }

    [Fact]
    public void CreateFromOrder_ExceedingLimit_Throws()
    {
        var act = () => InvoiceEntity.CreateFromOrder(Guid.NewGuid(), "Khách", 1_000_000_001m);
        act.Should().Throw<DomainException>();
    }

    // ---------- OrderConfirmedConsumer (Invoice.API) ----------

    [Fact]
    public async Task OrderConfirmedConsumer_CreatesInvoice_LinkedToOrder_AndPublishesInvoiceCreatedEvent()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(connection);

        var orderId = Guid.NewGuid();
        var message = new OrderConfirmedEvent(orderId, Guid.NewGuid(), "Khách B", 1200m, [], DateTime.UtcNow);

        var publishMock = new Mock<IPublishEndpoint>();
        IInvoiceCreatedEvent? published = null;
        publishMock
            .Setup(p => p.Publish<IInvoiceCreatedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((values, _) => published = Mock.Of<IInvoiceCreatedEvent>(m =>
                m.Id == (Guid)values.GetType().GetProperty("Id")!.GetValue(values)! &&
                m.CustomerName == (string)values.GetType().GetProperty("CustomerName")!.GetValue(values)! &&
                m.Amount == (decimal)values.GetType().GetProperty("Amount")!.GetValue(values)!))
            .Returns(Task.CompletedTask);

        var consumer = new OrderConfirmedConsumer(context, publishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<OrderConfirmedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        var invoice = await context.Invoices.SingleAsync(i => i.OrderId == orderId);
        invoice.CustomerName.Should().Be("Khách B");
        invoice.Amount.Should().Be(1200m);
        invoice.Status.Should().Be(Bizcore.BuildingBlocks.InvoiceStatus.Paid);

        published.Should().NotBeNull();
        published!.Id.Should().Be(invoice.Id);
        published.Amount.Should().Be(1200m);
    }

    [Fact]
    public async Task OrderConfirmedConsumer_WhenInvoiceAlreadyExistsForOrder_DoesNotDuplicate()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInvoiceDbContext(connection);

        var orderId = Guid.NewGuid();
        context.Invoices.Add(InvoiceEntity.CreateFromOrder(orderId, "Khách C", 300m));
        await context.SaveChangesAsync();

        var message = new OrderConfirmedEvent(orderId, Guid.NewGuid(), "Khách C", 300m, [], DateTime.UtcNow);
        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);

        var consumer = new OrderConfirmedConsumer(context, publishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<OrderConfirmedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        context.Invoices.Count(i => i.OrderId == orderId).Should().Be(1);
    }
}
