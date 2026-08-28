using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using CustomerOrderConfirmedConsumer = Customer.API.Application.Consumers.OrderConfirmedConsumer;
using CustomerOrderConfirmedFaultConsumer = Customer.API.Application.Consumers.OrderConfirmedFaultConsumer;
using InventoryOrderRevertedConsumer = Inventory.API.Application.Consumers.OrderRevertedConsumer;
using InventoryStock = Inventory.API.Domain.Entities.Stock;
using OrderEntity = Order.API.Domain.Entities.Order;
using OrderPaymentCompensationConsumer = Order.API.Application.Consumers.PaymentCompensationRequestedConsumer;

namespace Bizcore.UnitTests;

/// <summary>
/// Test đầu-cuối (nối cả 3 consumer thật, không mock lẫn nhau) mô phỏng ĐÚNG kịch bản: đơn hàng đã
/// thanh toán thành công nhưng bước cộng điểm khách hàng ở Customer.API bị lỗi VĨNH VIỄN (hết
/// retry) — ghi rõ trạng thái Order.Status ở TỪNG GIAI ĐOẠN của toàn bộ chuỗi bồi hoàn.
///
/// Không dùng MassTransit test harness thật (chờ đủ 5 lần retry ~8.7s theo Intervals(200,500,1000,
/// 2000,5000) sẽ làm test chậm không cần thiết) — thay vào đó dựng trực tiếp Fault&lt;OrderConfirmedEvent&gt;
/// ở giai đoạn "hết retry", đúng những gì MassTransit sẽ tự tạo ra trong thực tế.
/// </summary>
public class PointsAwardCompensationEndToEndTests
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
    public async Task FullFlow_PaymentSucceeded_PointsAwardPermanentlyFails_OrderRevertsToExactPreviousState()
    {
        // ═══════════════════════════════════════════════════════════════════════════════════════
        // GIAI ĐOẠN 0 — Trước khi đơn được thanh toán: đơn ở trạng thái Pending (mặc định lúc tạo).
        // ═══════════════════════════════════════════════════════════════════════════════════════
        var productId = Guid.NewGuid();
        const int quantity = 2;

        using var orderConnection = TestDbContextFactory.CreateOpenConnection();
        using var orderContext = TestDbContextFactory.CreateOrderDbContext(orderConnection);

        var order = OrderEntity.Create(Guid.NewGuid(), "Khách A", null, [(productId, "SP", quantity, 800_000m)]);
        order.Status.Should().Be(OrderStatus.Pending, "Giai đoạn 0: đơn vừa tạo, chưa thanh toán");
        orderContext.Orders.Add(order);
        await orderContext.SaveChangesAsync();

        // Kho tương ứng: đã Reserve lúc tạo đơn (OrderCreatedEvent), khớp Giai đoạn 0.
        using var inventoryConnection = TestDbContextFactory.CreateOpenConnection();
        using var inventoryContext = TestDbContextFactory.CreateInventoryDbContext(inventoryConnection);
        var stock = InventoryStock.Create(productId, "SP", initialOnHand: 10);
        stock.Reserve(quantity);
        inventoryContext.Stocks.Add(stock);
        await inventoryContext.SaveChangesAsync();
        stock.QuantityOnHand.Should().Be(10, "Giai đoạn 0: mới Reserve, chưa Commit nên OnHand chưa đổi");
        stock.QuantityReserved.Should().Be(quantity, "Giai đoạn 0: đã giữ chỗ cho đơn Pending");

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // GIAI ĐOẠN 1 — Thanh toán thành công: Payment.API Completed, Order.API tự Confirm() qua
        // PaymentConfirmedConsumer (không mô phỏng lại toàn bộ saga ở đây — chỉ áp dụng đúng hệ quả
        // của nó: Order.Status chuyển Pending -> Confirmed), Inventory.API Commit() (trừ kho thật).
        // ═══════════════════════════════════════════════════════════════════════════════════════
        order.Confirm();
        await orderContext.SaveChangesAsync();
        order.Status.Should().Be(OrderStatus.Confirmed, "Giai đoạn 1: thanh toán đã thành công, đơn đã xác nhận");

        stock.Commit(quantity);
        await inventoryContext.SaveChangesAsync();
        stock.QuantityOnHand.Should().Be(8, "Giai đoạn 1: Commit trừ kho thật");
        stock.QuantityReserved.Should().Be(0, "Giai đoạn 1: Commit giải phóng luôn phần giữ chỗ");

        var paymentId = Guid.NewGuid();
        var orderConfirmedEvent = new OrderConfirmedEvent(
            order.Id, order.CustomerId, order.CustomerName, order.TotalAmount,
            [new OrderEventItem(productId, quantity)], DateTime.UtcNow, PaymentId: paymentId);

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // GIAI ĐOẠN 2 — Customer.API nhận OrderConfirmedEvent, cố cộng điểm nhưng lỗi DB (mô phỏng
        // 1 trong 5 lần thử — MassTransit sẽ tự retry, KHÔNG bồi hoàn ở bước này).
        // ═══════════════════════════════════════════════════════════════════════════════════════
        using var customerConnection = TestDbContextFactory.CreateOpenConnection();
        using var customerContext = TestDbContextFactory.CreateCustomerDbContext(customerConnection);
        customerConnection.Close(); // giả lập lỗi hạ tầng khi SaveChangesAsync

        var pointsConsumer = new CustomerOrderConfirmedConsumer(
            customerContext, Mock.Of<IPublishEndpoint>(), Mock.Of<IAuditPublisher>(), NullLogger<CustomerOrderConfirmedConsumer>.Instance);
        var attempt = async () => await pointsConsumer.Consume(BuildConsumeContext(orderConfirmedEvent).Object);
        await attempt.Should().ThrowAsync<Exception>("1 lần thử lỗi phải throw để MassTransit tự retry");

        order.Status.Should().Be(OrderStatus.Confirmed,
            "Giai đoạn 2: mới lỗi 1 lần thử, CHƯA hết retry — Order/Payment không bị đụng tới");

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // GIAI ĐOẠN 3 — Giả định cả 5 lần thử đều lỗi (lỗi vĩnh viễn). MassTransit tự publish
        // Fault<OrderConfirmedEvent> → OrderConfirmedFaultConsumer yêu cầu bồi hoàn thanh toán.
        // ═══════════════════════════════════════════════════════════════════════════════════════
        var fault = Mock.Of<Fault<OrderConfirmedEvent>>(f => f.Message == orderConfirmedEvent);
        IPaymentCompensationRequestedEvent? compensationRequested = null;
        var publishMock = new Mock<IPublishEndpoint>();
        publishMock
            .Setup(p => p.Publish<IPaymentCompensationRequestedEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((values, _) =>
            {
                var t = values.GetType();
                compensationRequested = Mock.Of<IPaymentCompensationRequestedEvent>(m =>
                    m.PaymentId == (Guid)t.GetProperty("PaymentId")!.GetValue(values)! &&
                    m.OrderId == (Guid?)t.GetProperty("OrderId")!.GetValue(values) &&
                    m.Reason == (string)t.GetProperty("Reason")!.GetValue(values)!);
            })
            .Returns(Task.CompletedTask);

        var faultConsumer = new CustomerOrderConfirmedFaultConsumer(publishMock.Object, NullLogger<CustomerOrderConfirmedFaultConsumer>.Instance);
        await faultConsumer.Consume(BuildConsumeContext(fault).Object);

        compensationRequested.Should().NotBeNull("Giai đoạn 3: hết retry phải yêu cầu bồi hoàn");
        compensationRequested!.OrderId.Should().Be(order.Id);
        order.Status.Should().Be(OrderStatus.Confirmed,
            "Giai đoạn 3: MỚI yêu cầu bồi hoàn (publish event) — Order.API CHƯA xử lý nên vẫn Confirmed");

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // GIAI ĐOẠN 4 — Order.API nhận yêu cầu bồi hoàn, gọi Order.Revert(): trả đơn về ĐÚNG trạng
        // thái trước khi xử lý thanh toán (Pending — Giai đoạn 0), không phải Cancelled, đồng thời
        // publish OrderRevertedEvent để Inventory Service nhập lại kho.
        // ═══════════════════════════════════════════════════════════════════════════════════════
        OrderRevertedEvent? orderReverted = null;
        var orderPublishMock = new Mock<IPublishEndpoint>();
        orderPublishMock
            .Setup(p => p.Publish(It.IsAny<OrderRevertedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OrderRevertedEvent, CancellationToken>((e, _) => orderReverted = e)
            .Returns(Task.CompletedTask);

        var compensationConsumer = new OrderPaymentCompensationConsumer(
            orderContext, orderPublishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<OrderPaymentCompensationConsumer>.Instance);
        await compensationConsumer.Consume(BuildConsumeContext(compensationRequested).Object);

        var finalOrder = await orderContext.Orders.SingleAsync(o => o.Id == order.Id);
        finalOrder.Status.Should().Be(OrderStatus.Pending,
            "Giai đoạn 4: bồi hoàn xong — đơn trả về ĐÚNG trạng thái Pending như Giai đoạn 0, không phải Cancelled");
        finalOrder.CancelReason.Should().BeNull("Revert() không phải hành động hủy nên không ghi CancelReason");
        orderReverted.Should().NotBeNull("phải báo cho Inventory Service nhập lại kho đã Commit");

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // GIAI ĐOẠN 5 — Inventory.API nhận OrderRevertedEvent, Uncommit(): nhập lại kho về ĐÚNG số
        // liệu Giai đoạn 0 (trước khi Confirm/Commit) — trả lời trực tiếp câu hỏi "kho có được xử lý
        // lại ban đầu không": CÓ, đối xứng hoàn toàn với việc Order/Payment cũng được trả về ban đầu.
        // ═══════════════════════════════════════════════════════════════════════════════════════
        var inventoryRevertConsumer = new InventoryOrderRevertedConsumer(inventoryContext, NullLogger<InventoryOrderRevertedConsumer>.Instance);
        await inventoryRevertConsumer.Consume(BuildConsumeContext(orderReverted!).Object);

        var finalStock = inventoryContext.Stocks.Single(s => s.ProductId == productId);
        finalStock.QuantityOnHand.Should().Be(10, "Giai đoạn 5: OnHand trả về ĐÚNG như Giai đoạn 0 (trước Commit)");
        finalStock.QuantityReserved.Should().Be(quantity, "Giai đoạn 5: Reserved trả về ĐÚNG như Giai đoạn 0 — đơn Pending vẫn giữ chỗ, có thể thanh toán lại");

        // ═══════════════════════════════════════════════════════════════════════════════════════
        // GIAI ĐOẠN 6 — Phía Customer.API: khách hàng CHƯA được cộng điểm (không có trạng thái dở
        // dang) — nhất quán với việc Order/Payment/Inventory đều đã được trả về ban đầu, không có
        // bên nào "thắng" một phần.
        // ═══════════════════════════════════════════════════════════════════════════════════════
        using var verifyCustomerConnection = TestDbContextFactory.CreateOpenConnection();
        using var verifyCustomerContext = TestDbContextFactory.CreateCustomerDbContext(verifyCustomerConnection);
        verifyCustomerContext.CustomerPointsTransactions.Should().BeEmpty(
            "Giai đoạn 6: cộng điểm chưa từng thành công lần nào trong suốt chuỗi trên");
    }
}
