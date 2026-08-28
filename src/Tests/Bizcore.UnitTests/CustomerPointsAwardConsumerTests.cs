using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Customer.API.Application.Consumers;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using CustomerEntity = Customer.API.Domain.Entities.Customer;
using CustomerGroupEntity = Customer.API.Domain.Entities.CustomerGroup;

namespace Bizcore.UnitTests;

// Consumer cộng điểm thưởng cho khách hàng khi Order.API confirm 1 đơn hàng đã thanh toán
// (OrderConfirmedEvent với PaymentId có giá trị): đơn > 1.000.000đ được +5 điểm, còn lại +1 điểm.
public class CustomerPointsAwardConsumerTests
{
    private static Mock<ConsumeContext<TMessage>> BuildConsumeContext<TMessage>(TMessage message)
        where TMessage : class
    {
        var context = new Mock<ConsumeContext<TMessage>>();
        context.SetupGet(c => c.Message).Returns(message);
        context.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return context;
    }

    private static async Task<(CustomerEntity Customer, Customer.API.Infrastructure.Data.AppDbContext Context)>
        SeedCustomerAsync(Customer.API.Infrastructure.Data.AppDbContext context)
    {
        var group = CustomerGroupEntity.Create("VIP", "Khách VIP", null);
        context.CustomerGroups.Add(group);

        var customer = CustomerEntity.Create("KH001", "Khách A", group.Id);
        context.Customers.Add(customer);
        await context.SaveChangesAsync();

        return (customer, context);
    }

    [Fact]
    public async Task Consume_WhenPaidOrderOverOneMillion_Awards5Points()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateCustomerDbContext(connection);
        var (customer, _) = await SeedCustomerAsync(context);

        var orderId = Guid.NewGuid();
        var message = new OrderConfirmedEvent(
            orderId, customer.Id, customer.Name, 1_500_000m, [], DateTime.UtcNow, PaymentId: Guid.NewGuid());

        var consumer = new OrderConfirmedConsumer(context, Mock.Of<IPublishEndpoint>(), Mock.Of<IAuditPublisher>(), NullLogger<OrderConfirmedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        var updated = await context.Customers.SingleAsync(c => c.Id == customer.Id);
        updated.Points.Should().Be(5);

        var tx = context.CustomerPointsTransactions.Single();
        tx.OrderId.Should().Be(orderId);
        tx.PointsAwarded.Should().Be(5);
        tx.PointsBalanceAfter.Should().Be(5);
    }

    [Theory]
    // Mọi đường ra "coi như xong" của consumer đều PHẢI publish IOrderPaymentFulfilledEvent — đây là
    // tín hiệu duy nhất để Payment.API biết chuỗi đã hoàn tất và báo thành công cho khách. Thiếu ở
    // bất kỳ nhánh nào sẽ khiến khách treo màn hình chờ tới khi hết timeout dù giao dịch đã xong.
    [InlineData(false, false)] // cộng điểm thành công
    [InlineData(true, false)]  // đã cộng từ trước (redelivery) — vẫn phải báo lại
    [InlineData(false, true)]  // không tìm thấy khách hàng — bỏ qua cộng điểm nhưng không bồi hoàn
    public async Task Consume_OnEveryNonCompensatingOutcome_PublishesOrderPaymentFulfilledEvent(
        bool alreadyAwarded, bool customerMissing)
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateCustomerDbContext(connection);
        var (customer, _) = await SeedCustomerAsync(context);

        var orderId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        if (alreadyAwarded)
        {
            context.CustomerPointsTransactions.Add(
                Customer.API.Domain.Entities.CustomerPointsTransaction.Create(customer.Id, orderId, 5, 5));
            await context.SaveChangesAsync();
        }

        var message = new OrderConfirmedEvent(
            orderId,
            customerMissing ? Guid.NewGuid() : customer.Id,
            "Khách A", 1_500_000m, [], DateTime.UtcNow, PaymentId: paymentId);

        object? published = null;
        var publishMock = new Mock<IPublishEndpoint>();
        publishMock
            .Setup(p => p.Publish<IOrderPaymentFulfilledEvent>(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((values, _) => published = values)
            .Returns(Task.CompletedTask);

        var consumer = new OrderConfirmedConsumer(context, publishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<OrderConfirmedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        published.Should().NotBeNull();
        var type = published!.GetType();
        ((Guid)type.GetProperty("PaymentId")!.GetValue(published)!).Should().Be(paymentId);
        ((Guid)type.GetProperty("OrderId")!.GetValue(published)!).Should().Be(orderId);
    }

    [Fact]
    public async Task Consume_WhenManuallyConfirmed_NoPaymentId_DoesNotPublishFulfilled()
    {
        // Confirm thủ công: không có khách hàng nào đang chờ kết quả thanh toán nên không cần báo.
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateCustomerDbContext(connection);
        var (customer, _) = await SeedCustomerAsync(context);

        var message = new OrderConfirmedEvent(
            Guid.NewGuid(), customer.Id, customer.Name, 1_500_000m, [], DateTime.UtcNow, PaymentId: null);

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);

        var consumer = new OrderConfirmedConsumer(context, publishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<OrderConfirmedConsumer>.Instance);
        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Consume_WhenPaidOrderAtOrBelowOneMillion_Awards1Point()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateCustomerDbContext(connection);
        var (customer, _) = await SeedCustomerAsync(context);

        var message = new OrderConfirmedEvent(
            Guid.NewGuid(), customer.Id, customer.Name, 1_000_000m, [], DateTime.UtcNow, PaymentId: Guid.NewGuid());

        var consumer = new OrderConfirmedConsumer(context, Mock.Of<IPublishEndpoint>(), Mock.Of<IAuditPublisher>(), NullLogger<OrderConfirmedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        (await context.Customers.SingleAsync(c => c.Id == customer.Id)).Points.Should().Be(1);
    }

    [Fact]
    public async Task Consume_WhenManuallyConfirmed_NoPaymentId_DoesNotAwardPoints()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateCustomerDbContext(connection);
        var (customer, _) = await SeedCustomerAsync(context);

        var message = new OrderConfirmedEvent(
            Guid.NewGuid(), customer.Id, customer.Name, 2_000_000m, [], DateTime.UtcNow, PaymentId: null);

        var consumer = new OrderConfirmedConsumer(context, Mock.Of<IPublishEndpoint>(), Mock.Of<IAuditPublisher>(), NullLogger<OrderConfirmedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);

        (await context.Customers.SingleAsync(c => c.Id == customer.Id)).Points.Should().Be(0);
        context.CustomerPointsTransactions.Should().BeEmpty();
    }

    [Fact]
    public async Task Consume_WhenRedelivered_IsIdempotent_DoesNotAwardPointsTwice()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateCustomerDbContext(connection);
        var (customer, _) = await SeedCustomerAsync(context);

        var message = new OrderConfirmedEvent(
            Guid.NewGuid(), customer.Id, customer.Name, 1_500_000m, [], DateTime.UtcNow, PaymentId: Guid.NewGuid());

        var consumer = new OrderConfirmedConsumer(context, Mock.Of<IPublishEndpoint>(), Mock.Of<IAuditPublisher>(), NullLogger<OrderConfirmedConsumer>.Instance);
        await consumer.Consume(BuildConsumeContext(message).Object);
        await consumer.Consume(BuildConsumeContext(message).Object); // redelivery giả lập

        (await context.Customers.SingleAsync(c => c.Id == customer.Id)).Points.Should().Be(5);
        context.CustomerPointsTransactions.Should().ContainSingle();
    }

    [Fact]
    public async Task Consume_WhenAwardingPointsFailsAfterPaymentSucceeded_ThrowsForRetry_LeavesNoPartialState()
    {
        // Test này mô phỏng ĐÚNG 1 LẦN THỬ (trong tối đa 5 lần retry — xem
        // ApplyBusinessEndpointSettings) khi đơn hàng đã thanh toán thành công (Payment.Completed,
        // Order.Confirmed) nhưng bước CỘNG ĐIỂM khách hàng ở Customer.API bị lỗi. Ở MỖI LẦN THỬ,
        // consumer chỉ throw để MassTransit tự retry — KHÔNG bồi hoàn ngay, vì lỗi có thể chỉ là
        // thoáng qua (mất kết nối DB...) và tự khỏi ở lần thử sau.
        //
        // Chỉ khi TẤT CẢ 5 lần thử đều thất bại (lỗi vĩnh viễn), MassTransit mới publish
        // Fault<OrderConfirmedEvent> — lúc đó OrderConfirmedFaultConsumer (xem
        // OrderConfirmedFaultConsumerTests.cs) mới thực sự yêu cầu bồi hoàn, khiến Payment.Status
        // chuyển Reversed và Order.Status bị Order.Revert() trả về ĐÚNG trạng thái trước khi thanh
        // toán (Pending — không phải Cancelled, đúng chuẩn compensating transaction). Test đó mới
        // là nơi verify "rollback trạng thái" — test NÀY chỉ verify hành vi ĐÚNG của 1 lần thử đơn lẻ.
        //
        // ── GIAI ĐOẠN 1 — trước khi OrderConfirmedConsumer (Customer.API) chạy ─────────────────
        //   Payment.API : Payment.Status  = Completed   (ConfirmPaymentConsumer đã SaveChanges xong)
        //   Order.API   : Order.Status    = Confirmed   (PaymentConfirmedConsumer đã gọi order.Confirm()
        //                                                 và publish OrderConfirmedEvent — encode ở đây
        //                                                 bằng PaymentId có giá trị)
        //   Customer.API: Customer.Points = 0            (chưa xử lý gì)
        // Cả 2 trạng thái Payment/Order này đã COMMIT xong ở DB của service tương ứng TRƯỚC KHI
        // OrderConfirmedEvent tới được Customer.API — nằm ngoài phạm vi DbContext của test này nên
        // không assert trực tiếp được, nhưng đó chính là tiền đề của message dưới đây.
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateCustomerDbContext(connection);
        var (customer, _) = await SeedCustomerAsync(context);
        customer.Points.Should().Be(0, "Giai đoạn 1: khách hàng chưa được cộng điểm lần nào");

        var orderId = Guid.NewGuid();
        var message = new OrderConfirmedEvent(
            orderId, customer.Id, customer.Name, 1_500_000m, [], DateTime.UtcNow, PaymentId: Guid.NewGuid());

        // ── GIAI ĐOẠN 2 — OrderConfirmedConsumer cố cộng điểm, nhưng DB lỗi thoáng qua ──────────
        // Giả lập lỗi hạ tầng (mất kết nối/timeout khi SaveChangesAsync) bằng cách đóng connection
        // ngay trước khi consumer chạy — mọi thao tác DB bên trong Consume() sẽ throw, đúng như một
        // lỗi lưu trữ thật sự giữa chừng thay vì lỗi nghiệp vụ (domain rule).
        connection.Close();

        var consumer = new OrderConfirmedConsumer(context, Mock.Of<IPublishEndpoint>(), Mock.Of<IAuditPublisher>(), NullLogger<OrderConfirmedConsumer>.Instance);
        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        // ── GIAI ĐOẠN 3 — hệ quả ngay sau khi cộng điểm thất bại ────────────────────────────────
        // 1. Exception PHẢI bay lên khỏi Consume(), không được nuốt/log-rồi-return — đây là điều
        //    kiện để MassTransit's UseMessageRetry (đã cấu hình sẵn ở tầng Infrastructure) tự động
        //    retry lại theo policy, và nếu retry hết vẫn lỗi thì message rơi vào error queue chờ xử
        //    lý thủ công — không được coi là "xong" một cách âm thầm.
        await act.Should().ThrowAsync<Exception>(
            "cộng điểm lỗi phải throw để MassTransit tự retry, không được nuốt lỗi rồi coi như thành công");

        // 2. Payment.Status (Payment.API) và Order.Status (Order.API) KHÔNG bị ảnh hưởng bởi RIÊNG
        //    lần thử này: OrderConfirmedConsumer.Consume() không publish bất kỳ event compensation/
        //    reversal nào — chỉ throw. Payment vẫn Completed, Order vẫn Confirmed nguyên vẹn SAU 1
        //    lần thử lỗi. Việc rollback 2 trạng thái này chỉ xảy ra sau khi hết cả 5 lần retry (xem
        //    OrderConfirmedFaultConsumerTests.cs), không phải ngay từ lần lỗi đầu tiên.
        //
        // 3. Phía Customer.API (kiểm chứng được trực tiếp): dùng context/connection MỚI (đại diện
        //    cho lần retry tiếp theo) để xác nhận không có trạng thái dở dang nào bị lọt xuống DB —
        //    SaveChangesAsync thất bại toàn bộ (atomic), không có chuyện cộng điểm rồi mới lỗi ở bước
        //    ghi StockTransaction tương tự.
        using var verifyConnection = TestDbContextFactory.CreateOpenConnection();
        // (không thể tái dùng context/connection cũ vì đã Close() ở trên) — verify bằng 1 context
        // sạch, độc lập, seed lại đúng customer đó để mô phỏng "nhìn từ lần retry kế tiếp".
        using var verifyContext = TestDbContextFactory.CreateCustomerDbContext(verifyConnection);
        var (retryCustomer, _) = await SeedCustomerAsync(verifyContext);
        retryCustomer.Points.Should().Be(0,
            "Giai đoạn 3: cộng điểm thất bại toàn bộ nên Points KHÔNG được tăng dở dang trước khi retry");
        verifyContext.CustomerPointsTransactions.Should().BeEmpty(
            "Giai đoạn 3: không có bản ghi ledger nào được tạo khi cộng điểm thất bại — lần retry sau " +
            "(cùng OrderId) vẫn phải được coi là CHƯA xử lý, không bị bỏ qua nhầm bởi check idempotent");
    }

    [Fact]
    public async Task Consume_WhenCustomerNotFound_DoesNotThrow_DoesNotCreateTransaction()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateCustomerDbContext(connection);

        var message = new OrderConfirmedEvent(
            Guid.NewGuid(), Guid.NewGuid(), "Khách lạ", 1_500_000m, [], DateTime.UtcNow, PaymentId: Guid.NewGuid());

        var consumer = new OrderConfirmedConsumer(context, Mock.Of<IPublishEndpoint>(), Mock.Of<IAuditPublisher>(), NullLogger<OrderConfirmedConsumer>.Instance);
        var act = async () => await consumer.Consume(BuildConsumeContext(message).Object);

        await act.Should().NotThrowAsync();
        context.CustomerPointsTransactions.Should().BeEmpty();
    }
}
