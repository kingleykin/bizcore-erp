using Bizcore.ApiTests.Infrastructure;
using Bizcore.BuildingBlocks.Contracts;
using Customer.API.Infrastructure.Data;
using FluentAssertions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CustomerEntity = Customer.API.Domain.Entities.Customer;
using CustomerGroupEntity = Customer.API.Domain.Entities.CustomerGroup;

namespace Bizcore.ApiTests.Services;

/// <summary>
/// Test tích hợp thật (SQL Server + RabbitMQ qua Testcontainers, không mock DbContext/MassTransit)
/// cho luồng cộng điểm khách hàng khi Order được Confirm do thanh toán — bù cho khoảng trống mà
/// Bizcore.UnitTests không phủ được: consumer có thực sự được MassTransit đăng ký/route đúng
/// không, migration (đặc biệt unique index OrderId trên CustomerPointsTransactions) có thực sự áp
/// dụng đúng trên SQL Server không, message có thực sự đi qua RabbitMQ rồi được xử lý không — toàn
/// bộ những thứ mock ở unit test không thể phát hiện sai.
///
/// Chỉ kiểm tra luồng trong PHẠM VI Customer.API (publish thẳng OrderConfirmedEvent lên bus thật,
/// không dựng lại toàn bộ Order.API/Payment.API/Orchestration.API) — ApiTestBase&lt;TEntryPoint&gt;
/// hiện chỉ host 1 service/lần nên việc dựng đồng thời nhiều service thật cùng nói chuyện qua
/// RabbitMQ cần hạ tầng test mới (nhiều WebApplicationFactory dùng chung 1 RabbitMqContainer, mỗi
/// service 1 database riêng) — chưa có trong ApiTestBase hiện tại, để lại như một việc riêng.
/// </summary>
public class CustomerPointsAwardApiTests : ApiTestBase<Customer.API.Program>
{
    private static async Task<CustomerEntity> SeedCustomerAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var group = CustomerGroupEntity.Create($"GRP{Guid.NewGuid():N}"[..10], "Nhóm test", null);
        db.CustomerGroups.Add(group);

        var customer = CustomerEntity.Create($"KH{Guid.NewGuid():N}"[..10], "Khách hàng test", group.Id);
        db.Customers.Add(customer);

        await db.SaveChangesAsync();
        return customer;
    }

    private static async Task<int?> PollCustomerPointsAsync(IServiceProvider services, Guid customerId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow.Add(timeout);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var points = await db.Customers.AsNoTracking()
                .Where(c => c.Id == customerId)
                .Select(c => (int?)c.Points)
                .FirstOrDefaultAsync();

            if (points is > 0)
                return points;

            await Task.Delay(500);
        }

        return null;
    }

    [Fact]
    public async Task OrderConfirmedEvent_WithPaymentId_OverOneMillion_RealBus_AwardsFivePointsInRealDatabase()
    {
        var customer = await SeedCustomerAsync(_factory.Services);

        var orderId = Guid.NewGuid();
        using (var publishScope = _factory.Services.CreateScope())
        {
            var publishEndpoint = publishScope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            await publishEndpoint.Publish(new OrderConfirmedEvent(
                orderId, customer.Id, customer.Name, 1_500_000m, [], DateTime.UtcNow, PaymentId: Guid.NewGuid()));
        }

        var points = await PollCustomerPointsAsync(_factory.Services, customer.Id, TimeSpan.FromSeconds(30));

        points.Should().Be(5, "đơn > 1.000.000đ phải được +5 điểm — xác nhận qua RabbitMQ + SQL Server thật, " +
                              "không phải DbContext mock");

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tx = await verifyDb.CustomerPointsTransactions.AsNoTracking()
            .SingleOrDefaultAsync(t => t.OrderId == orderId);
        tx.Should().NotBeNull("phải có 1 bản ghi ledger tương ứng — xác nhận migration/unique index thật hoạt động đúng");
        tx!.PointsAwarded.Should().Be(5);
    }

    [Fact]
    public async Task OrderConfirmedEvent_WithoutPaymentId_RealBus_DoesNotAwardPoints()
    {
        var customer = await SeedCustomerAsync(_factory.Services);

        using (var publishScope = _factory.Services.CreateScope())
        {
            var publishEndpoint = publishScope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
            await publishEndpoint.Publish(new OrderConfirmedEvent(
                Guid.NewGuid(), customer.Id, customer.Name, 2_000_000m, [], DateTime.UtcNow, PaymentId: null));
        }

        // Không có tín hiệu "đã xử lý xong" để chờ (đúng ý đồ: consumer return sớm, không ghi gì) —
        // chờ 1 khoảng đủ dài hơn hẳn thời gian xử lý bình thường rồi assert KHÔNG có gì thay đổi.
        await Task.Delay(TimeSpan.FromSeconds(5));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Customers.AsNoTracking().SingleAsync(c => c.Id == customer.Id)).Points.Should().Be(0,
            "Confirm thủ công (không có PaymentId) không được cộng điểm");
    }
}
