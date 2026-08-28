using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Exceptions;
using FluentAssertions;
using Inventory.API.Application.Consumers;
using Inventory.API.Domain.Entities;
using Inventory.API.Infrastructure.Data;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Bizcore.UnitTests;

public class InventoryConsumersTests
{
    private static Mock<ConsumeContext<TMessage>> BuildConsumeContext<TMessage>(TMessage message)
        where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    private static OrderEventItem Item(Guid productId, int quantity) => new(productId, quantity);

    // ---------- OrderCreatedConsumer ----------

    [Fact]
    public async Task OrderCreatedConsumer_WhenStockExists_ReservesAndLogsTransaction()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        context.Stocks.Add(Stock.Create(productId, "Bàn phím", 50));
        await context.SaveChangesAsync();

        var orderId = Guid.NewGuid();
        var message = new OrderCreatedEvent(
            orderId, Guid.NewGuid(), "Khách A", "ORD001", 100m,
            new List<OrderEventItem> { Item(productId, 5) }, DateTime.UtcNow);

        var consumer = new OrderCreatedConsumer(context, NullLogger<OrderCreatedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        var stock = context.Stocks.Single(s => s.ProductId == productId);
        stock.QuantityOnHand.Should().Be(50);
        stock.QuantityReserved.Should().Be(5);

        var tx = context.StockTransactions.Single();
        tx.Type.Should().Be(StockTransactionType.Reserve);
        tx.Quantity.Should().Be(5);
        tx.RelatedOrderId.Should().Be(orderId);
        tx.QuantityOnHandAfter.Should().Be(50);
        tx.QuantityReservedAfter.Should().Be(5);
    }

    [Fact]
    public async Task OrderCreatedConsumer_WhenStockMissing_SelfHealsByCreatingZeroOnHand_ThenThrowsInsufficientStock()
    {
        // Order.API đã kiểm tra tồn kho khả dụng trước khi publish OrderCreatedEvent, nên tới đây
        // chỉ còn xảy ra do race condition. Stock tự tạo với OnHand=0 nhưng Reserve(3) phải throw
        // (0 available < 3 yêu cầu) thay vì âm thầm bán vượt tồn — không có gì được lưu xuống DB.
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        var message = new OrderCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Khách B", "ORD002", 50m,
            new List<OrderEventItem> { Item(productId, 3) }, DateTime.UtcNow);

        var consumer = new OrderCreatedConsumer(context, NullLogger<OrderCreatedConsumer>.Instance);
        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().ThrowAsync<DomainException>();
        context.Stocks.Should().BeEmpty("SaveChanges chưa từng được gọi vì Reserve throw trước đó");
        context.StockTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task OrderCreatedConsumer_WithMultipleItems_ReservesAllAndLogsEachTransaction()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        context.Stocks.Add(Stock.Create(productA, "A", 10));
        context.Stocks.Add(Stock.Create(productB, "B", 20));
        await context.SaveChangesAsync();

        var message = new OrderCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Khách C", "ORD003", 200m,
            new List<OrderEventItem> { Item(productA, 2), Item(productB, 4) }, DateTime.UtcNow);

        var consumer = new OrderCreatedConsumer(context, NullLogger<OrderCreatedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        context.Stocks.Single(s => s.ProductId == productA).QuantityReserved.Should().Be(2);
        context.Stocks.Single(s => s.ProductId == productB).QuantityReserved.Should().Be(4);
        context.StockTransactions.Count().Should().Be(2);
    }

    // ---------- OrderConfirmedConsumer ----------

    [Fact]
    public async Task OrderConfirmedConsumer_WhenStockExists_CommitsReducesOnHandAndReserved()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        var stock = Stock.Create(productId, "Bàn phím", 50);
        stock.Reserve(5);
        context.Stocks.Add(stock);
        await context.SaveChangesAsync();

        var orderId = Guid.NewGuid();
        var message = new OrderConfirmedEvent(
            orderId, "Khách", 500m, new List<OrderEventItem> { Item(productId, 5) }, DateTime.UtcNow);

        var consumer = new OrderConfirmedConsumer(context, Mock.Of<IPublishEndpoint>(), NullLogger<OrderConfirmedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        var updated = context.Stocks.Single(s => s.ProductId == productId);
        updated.QuantityOnHand.Should().Be(45);
        updated.QuantityReserved.Should().Be(0);

        var tx = context.StockTransactions.Single();
        tx.Type.Should().Be(StockTransactionType.Commit);
        tx.Quantity.Should().Be(-5, "Commit là xuất kho thật nên phải mang dấu âm trong lịch sử");
        tx.RelatedOrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task OrderConfirmedConsumer_WhenStockMissing_ThrowsToTriggerRetry()
    {
        // Regression test: trước đây consumer chỉ log warning rồi bỏ qua âm thầm khi
        // race condition xảy ra (OrderCreatedEvent chưa kịp xử lý) — khiến việc trừ kho
        // bị mất vĩnh viễn. Giờ phải throw để MassTransit UseMessageRetry retry lại.
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        var message = new OrderConfirmedEvent(
            Guid.NewGuid(), "Khách", 500m, new List<OrderEventItem> { Item(productId, 5) }, DateTime.UtcNow);

        var consumer = new OrderConfirmedConsumer(context, Mock.Of<IPublishEndpoint>(), NullLogger<OrderConfirmedConsumer>.Instance);

        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().ThrowAsync<InvalidOperationException>();
        context.StockTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task OrderConfirmedConsumer_WhenCommitExceedsReserved_RequestsCompensation_AndDoesNotMutateStock()
    {
        // Trường hợp hiếm: quantityReserved < quantity cần commit (race condition). Commit() throw
        // DomainException — consumer phải publish IPaymentCompensationRequestedEvent (vì đơn này
        // được Confirm tự động do thanh toán, PaymentId có giá trị) thay vì để Payment/Order/Invoice
        // kẹt ở trạng thái đã hoàn tất trong khi tồn kho chưa từng được trừ, và KHÔNG được để lọt
        // một phần thay đổi tồn kho nào xuống DB.
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        var stock = Stock.Create(productId, "Bàn phím", 50); // QuantityReserved = 0
        context.Stocks.Add(stock);
        await context.SaveChangesAsync();

        var paymentId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var message = new OrderConfirmedEvent(
            orderId, "Khách", 500m, new List<OrderEventItem> { Item(productId, 5) }, DateTime.UtcNow,
            PaymentId: paymentId);

        var publishMock = new Mock<IPublishEndpoint>();
        var consumer = new OrderConfirmedConsumer(context, publishMock.Object, NullLogger<OrderConfirmedConsumer>.Instance);

        await consumer.Consume(BuildConsumeContext(message).Object);

        var unchanged = context.Stocks.Single(s => s.ProductId == productId);
        unchanged.QuantityOnHand.Should().Be(50, "Commit thất bại thì không được trừ kho");
        unchanged.QuantityReserved.Should().Be(0);
        context.StockTransactions.Should().BeEmpty();

        publishMock.Verify(p => p.Publish<IPaymentCompensationRequestedEvent>(
            It.IsAny<object>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---------- OrderCancelledConsumer ----------

    [Fact]
    public async Task OrderCancelledConsumer_WhenStockExists_ReleasesReservedOnly()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        var stock = Stock.Create(productId, "Bàn phím", 50);
        stock.Reserve(5);
        context.Stocks.Add(stock);
        await context.SaveChangesAsync();

        var orderId = Guid.NewGuid();
        var message = new OrderCancelledEvent(
            orderId, new List<OrderEventItem> { Item(productId, 5) }, "Khách đổi ý", DateTime.UtcNow);

        var consumer = new OrderCancelledConsumer(context, NullLogger<OrderCancelledConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        var updated = context.Stocks.Single(s => s.ProductId == productId);
        updated.QuantityOnHand.Should().Be(50, "hủy đơn không đụng tới tồn kho vật lý");
        updated.QuantityReserved.Should().Be(0);

        var tx = context.StockTransactions.Single();
        tx.Type.Should().Be(StockTransactionType.Release);
        tx.Quantity.Should().Be(5);
        tx.RelatedOrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task OrderCancelledConsumer_WhenStockMissing_ThrowsToTriggerRetry()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        var message = new OrderCancelledEvent(
            Guid.NewGuid(), new List<OrderEventItem> { Item(productId, 5) }, "lý do", DateTime.UtcNow);

        var consumer = new OrderCancelledConsumer(context, NullLogger<OrderCancelledConsumer>.Instance);

        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ---------- ProductCreatedConsumer ----------

    [Fact]
    public async Task ProductCreatedConsumer_WhenNew_CreatesStockWithZeroOnHand()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        var message = Mock.Of<IProductCreatedEvent>(m =>
            m.Id == productId && m.Name == "Sản phẩm mới" && m.CreatedAt == DateTime.UtcNow);

        var consumer = new ProductCreatedConsumer(context, NullLogger<ProductCreatedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        var stock = context.Stocks.Single(s => s.ProductId == productId);
        stock.ProductName.Should().Be("Sản phẩm mới");
        stock.QuantityOnHand.Should().Be(0);
        stock.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public async Task ProductCreatedConsumer_WhenStockAlreadyExists_DoesNotDuplicate()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        context.Stocks.Add(Stock.Create(productId, "Sản phẩm cũ", 30));
        await context.SaveChangesAsync();

        var message = Mock.Of<IProductCreatedEvent>(m =>
            m.Id == productId && m.Name == "Sản phẩm cũ" && m.CreatedAt == DateTime.UtcNow);

        var consumer = new ProductCreatedConsumer(context, NullLogger<ProductCreatedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        context.Stocks.Count(s => s.ProductId == productId).Should().Be(1);
        context.Stocks.Single(s => s.ProductId == productId).QuantityOnHand.Should().Be(30);
    }
}
