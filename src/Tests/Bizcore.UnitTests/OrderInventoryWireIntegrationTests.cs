using Bizcore.BuildingBlocks.Contracts;
using FluentAssertions;
using Inventory.API.Application.Consumers;
using Inventory.API.Domain.Entities;
using Inventory.API.Infrastructure.Data;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Bizcore.UnitTests;

/// <summary>
/// Test end-to-end thật sự qua MassTransit test harness (publish → serialize → deserialize →
/// consume), KHÔNG mock IPublishEndpoint/ConsumeContext. Đây chính là bài test lẽ ra đã bắt được
/// bug gốc: khi 3 event Order (Created/Confirmed/Cancelled) còn khai báo dạng interface có property
/// collection lồng nhau (IReadOnlyCollection&lt;OrderEventItem&gt; Items), MassTransit's dynamic
/// interface-proxy không bind đúng property này — Items luôn null phía consumer dù publish "thành
/// công" ở mức unit test mock. Sau khi đổi 3 event sang record (concrete type), pipeline thật của
/// MassTransit xử lý đúng. Test dưới đây publish qua harness thật (có serialize/deserialize) rồi
/// khẳng định tồn kho được cập nhật đúng — nếu ai đó vô tình đổi event trở lại thành interface có
/// collection, test này phải fail.
/// </summary>
public class OrderInventoryWireIntegrationTests
{
    private static async Task<(ServiceProvider Provider, ITestHarness Harness, SqliteConnection Connection)> BuildHarnessAsync()
    {
        var connection = TestDbContextFactory.CreateOpenConnection();
        // EnsureCreated() chạy ngay trong CreateInventoryDbContext; dispose context tạm này để
        // nhả handle, còn connection SQLite in-memory vẫn giữ mở cho các DbContext khác dùng lại.
        using (TestDbContextFactory.CreateInventoryDbContext(connection)) { }

        var options = TestDbContextFactory.CreateInventoryDbContextOptions(connection);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<AppDbContext>(_ => new AppDbContext(options));

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<OrderCreatedConsumer>();
            x.AddConsumer<OrderConfirmedConsumer>();
            x.AddConsumer<OrderCancelledConsumer>();
        });

        var provider = services.BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        return (provider, harness, connection);
    }

    [Fact]
    public async Task OrderCreatedEvent_PublishedOverRealBus_IsConsumed_AndReservesStock()
    {
        var (provider, harness, connection) = await BuildHarnessAsync();
        try
        {
            var productId = Guid.NewGuid();
            using (var scope = provider.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                ctx.Stocks.Add(Stock.Create(productId, "Bàn phím", 50));
                await ctx.SaveChangesAsync();
            }

            var orderId = Guid.NewGuid();
            await harness.Bus.Publish(new OrderCreatedEvent(
                orderId, Guid.NewGuid(), "Khách Wire", "ORD-WIRE-1", 100m,
                new List<OrderEventItem> { new(productId, 7) }, DateTime.UtcNow));

            (await harness.Consumed.Any<OrderCreatedEvent>()).Should().BeTrue();

            using var assertScope = provider.CreateScope();
            var assertCtx = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stock = assertCtx.Stocks.Single(s => s.ProductId == productId);
            stock.QuantityReserved.Should().Be(7, "Items phải deserialize đúng qua wire thật, không còn null");
        }
        finally
        {
            await provider.DisposeAsync();
            connection.Dispose();
        }
    }

    [Fact]
    public async Task OrderConfirmedEvent_PublishedOverRealBus_IsConsumed_AndCommitsStock()
    {
        var (provider, harness, connection) = await BuildHarnessAsync();
        try
        {
            var productId = Guid.NewGuid();
            using (var scope = provider.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var stock = Stock.Create(productId, "Chuột", 30);
                stock.Reserve(6);
                ctx.Stocks.Add(stock);
                await ctx.SaveChangesAsync();
            }

            await harness.Bus.Publish(new OrderConfirmedEvent(
                Guid.NewGuid(), "Khách", 900m, new List<OrderEventItem> { new(productId, 6) }, DateTime.UtcNow));

            (await harness.Consumed.Any<OrderConfirmedEvent>()).Should().BeTrue();

            using var assertScope = provider.CreateScope();
            var assertCtx = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stock2 = assertCtx.Stocks.Single(s => s.ProductId == productId);
            stock2.QuantityOnHand.Should().Be(24);
            stock2.QuantityReserved.Should().Be(0);
        }
        finally
        {
            await provider.DisposeAsync();
            connection.Dispose();
        }
    }

    [Fact]
    public async Task OrderCancelledEvent_PublishedOverRealBus_IsConsumed_AndReleasesStock()
    {
        var (provider, harness, connection) = await BuildHarnessAsync();
        try
        {
            var productId = Guid.NewGuid();
            using (var scope = provider.CreateScope())
            {
                var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var stock = Stock.Create(productId, "Màn hình", 15);
                stock.Reserve(2);
                ctx.Stocks.Add(stock);
                await ctx.SaveChangesAsync();
            }

            await harness.Bus.Publish(new OrderCancelledEvent(
                Guid.NewGuid(), new List<OrderEventItem> { new(productId, 2) }, "khách hủy", DateTime.UtcNow));

            (await harness.Consumed.Any<OrderCancelledEvent>()).Should().BeTrue();

            using var assertScope = provider.CreateScope();
            var assertCtx = assertScope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stock2 = assertCtx.Stocks.Single(s => s.ProductId == productId);
            stock2.QuantityOnHand.Should().Be(15);
            stock2.QuantityReserved.Should().Be(0);
        }
        finally
        {
            await provider.DisposeAsync();
            connection.Dispose();
        }
    }
}
