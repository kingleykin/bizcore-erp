using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using Inventory.API.Application.Consumers;
using Inventory.API.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Bizcore.UnitTests;

// Consumer nhận OrderRevertedEvent — Order.API publish khi 1 đơn đã Confirm (đã Commit, trừ kho
// thật) bị bồi hoàn về Pending. Phải nhập lại ĐÚNG số lượng đã trừ, đối xứng với OrderConfirmedConsumer.
public class OrderRevertedConsumerTests
{
    private static Mock<ConsumeContext<TMessage>> BuildConsumeContext<TMessage>(TMessage message)
        where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    [Fact]
    public async Task Consume_RestoresOnHandAndReserved_ToExactStateBeforeCommit()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        var stock = Stock.Create(productId, "Bàn phím", 50);
        stock.Reserve(5);
        stock.Commit(5); // OnHand=45, Reserved=0 — trạng thái SAU khi đơn Confirm (đã trừ kho thật)
        context.Stocks.Add(stock);
        await context.SaveChangesAsync();

        var orderId = Guid.NewGuid();
        var message = new OrderRevertedEvent(
            orderId, new List<OrderEventItem> { new(productId, 5) }, "Cộng điểm thất bại vĩnh viễn", DateTime.UtcNow);

        var consumer = new OrderRevertedConsumer(context, NullLogger<OrderRevertedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        var updated = context.Stocks.Single(s => s.ProductId == productId);
        updated.QuantityOnHand.Should().Be(50, "phải trả OnHand về đúng trước lúc Commit");
        updated.QuantityReserved.Should().Be(5, "phải trả Reserved về đúng trước lúc Commit — đơn coi như Pending, vẫn giữ chỗ");

        var tx = context.StockTransactions.Single();
        tx.Type.Should().Be(StockTransactionType.Uncommit);
        tx.Quantity.Should().Be(5, "Uncommit là nhập lại kho nên mang dấu dương, ngược với Commit (âm)");
        tx.RelatedOrderId.Should().Be(orderId);
    }

    [Fact]
    public async Task Consume_WhenStockMissing_ThrowsToTriggerRetry()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        var message = new OrderRevertedEvent(
            Guid.NewGuid(), new List<OrderEventItem> { new(productId, 5) }, "lý do", DateTime.UtcNow);

        var consumer = new OrderRevertedConsumer(context, NullLogger<OrderRevertedConsumer>.Instance);
        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().ThrowAsync<InvalidOperationException>();
        context.StockTransactions.Should().BeEmpty();
    }
}
