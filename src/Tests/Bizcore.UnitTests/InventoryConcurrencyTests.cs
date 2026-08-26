using FluentAssertions;
using Inventory.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Bizcore.UnitTests;

/// <summary>
/// Stock kế thừa AggregateRoot nên Version được cấu hình làm optimistic-concurrency token
/// (EntityVersionInterceptor). Trong thực tế, nhiều đơn hàng có thể publish OrderCreated/
/// Confirmed/Cancelled gần như đồng thời cho CÙNG một sản phẩm (2 consumer instance xử lý
/// song song) — nếu không có concurrency check, một cập nhật có thể bị "lost update" (ghi đè
/// mất bản cập nhật của bên kia). Test này khẳng định EF thực sự phát hiện và chặn tình huống đó
/// bằng DbUpdateConcurrencyException thay vì âm thầm ghi đè.
/// </summary>
public class InventoryConcurrencyTests
{
    [Fact]
    public async Task ConcurrentUpdates_OnSameStock_SecondSaveThrowsDbUpdateConcurrencyException()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        var productId = Guid.NewGuid();

        using (var seedContext = TestDbContextFactory.CreateInventoryDbContext(connection))
        {
            seedContext.Stocks.Add(Stock.Create(productId, "Sản phẩm", 100));
            await seedContext.SaveChangesAsync();
        }

        // Hai "consumer instance" độc lập cùng load Stock ở Version giống nhau, mô phỏng 2 đơn hàng
        // được xử lý gần như đồng thời cho cùng 1 sản phẩm.
        using var contextA = TestDbContextFactory.CreateInventoryDbContext(connection);
        using var contextB = TestDbContextFactory.CreateInventoryDbContext(connection);

        var stockA = await contextA.Stocks.SingleAsync(s => s.ProductId == productId);
        var stockB = await contextB.Stocks.SingleAsync(s => s.ProductId == productId);

        stockA.Reserve(10);
        await contextA.SaveChangesAsync();

        stockB.Reserve(20); // contextB vẫn đang cầm Version cũ (trước khi contextA lưu)

        var act = async () => await contextB.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>(
            "contextB dùng Version đã lỗi thời — nếu không chặn được sẽ mất update của contextA (lost update)");
    }

    [Fact]
    public async Task SequentialUpdates_OnSameStock_BothSucceed_WhenReloadedBetween()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        var productId = Guid.NewGuid();

        using (var seedContext = TestDbContextFactory.CreateInventoryDbContext(connection))
        {
            seedContext.Stocks.Add(Stock.Create(productId, "Sản phẩm", 100));
            await seedContext.SaveChangesAsync();
        }

        using (var contextA = TestDbContextFactory.CreateInventoryDbContext(connection))
        {
            var stockA = await contextA.Stocks.SingleAsync(s => s.ProductId == productId);
            stockA.Reserve(10);
            await contextA.SaveChangesAsync();
        }

        using (var contextB = TestDbContextFactory.CreateInventoryDbContext(connection))
        {
            // Load LẠI sau khi contextA đã lưu — nhận Version mới nhất, không xung đột.
            var stockB = await contextB.Stocks.SingleAsync(s => s.ProductId == productId);
            stockB.Reserve(20);
            await contextB.SaveChangesAsync();
        }

        using var assertContext = TestDbContextFactory.CreateInventoryDbContext(connection);
        var final = await assertContext.Stocks.SingleAsync(s => s.ProductId == productId);
        final.QuantityReserved.Should().Be(30, "cả 2 lần giữ chỗ tuần tự phải cộng dồn đúng, không mất update nào");
    }
}
