using Bizcore.BuildingBlocks.Exceptions;
using FluentAssertions;
using Inventory.API.Application.Queries;
using Inventory.API.Domain.Entities;

namespace Bizcore.UnitTests;

/// <summary>
/// Test biên (boundary-value) cho nghiệp vụ tồn kho — các trường hợp "vừa đủ" mà logic
/// dễ off-by-one nhất: giữ chỗ/chốt/trả đúng bằng số hiện có.
/// </summary>
public class InventoryStockBoundaryTests
{
    private static readonly Guid ProductId = Guid.NewGuid();

    [Fact]
    public void Reserve_ExactlyEqualToAvailable_LeavesAvailableAtZero_NoOversell()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 10);

        stock.Reserve(10);

        stock.AvailableQuantity.Should().Be(0);
    }

    [Fact]
    public void Reserve_OneMoreThanAvailable_ThrowsInsufficientStock_NoOversell()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 10);

        var act = () => stock.Reserve(11);

        act.Should().Throw<DomainException>();
        stock.AvailableQuantity.Should().Be(10, "Reserve thất bại thì không được thay đổi trạng thái");
    }

    [Fact]
    public void Commit_ExactlyEqualToReserved_ZeroesOutReserved()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 10);
        stock.Reserve(10);

        stock.Commit(10);

        stock.QuantityOnHand.Should().Be(0);
        stock.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public void Commit_MoreThanReserved_ThrowsInsufficientStock_NoStateChange()
    {
        // Commit() giờ có guard chặn vượt quá QuantityReserved: nếu OrderConfirmed đến với số lượng
        // khác OrderCreated (không nên xảy ra trong luồng nghiệp vụ hợp lệ), phải throw thay vì để
        // QuantityReserved âm một cách âm thầm.
        var stock = Stock.Create(ProductId, "Sản phẩm", 10);
        stock.Reserve(5);

        var act = () => stock.Commit(8);

        act.Should().Throw<DomainException>();
        stock.QuantityReserved.Should().Be(5, "Commit thất bại thì không được thay đổi trạng thái");
        stock.QuantityOnHand.Should().Be(10);
    }

    [Fact]
    public void Release_ExactlyEqualToReserved_ZeroesOutReserved_ViaExactMatch_NotFloor()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 10);
        stock.Reserve(4);

        stock.Release(4);

        stock.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public void AdjustOnHand_ToZero_IsValidBoundary()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 10);

        stock.AdjustOnHand(0);

        stock.QuantityOnHand.Should().Be(0);
    }

    [Fact]
    public void Create_WithZeroInitialOnHand_IsValidBoundary()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 0);
        stock.QuantityOnHand.Should().Be(0);
        stock.AvailableQuantity.Should().Be(0);
    }

    [Fact]
    public async Task GetStockTransactionsQuery_WithMoreThan200Rows_CapsAt200_NewestFirst()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var baseTime = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        const int totalRows = 205;
        for (var i = 0; i < totalRows; i++)
        {
            var tx = StockTransaction.Create(ProductId, "Sản phẩm", StockTransactionType.Adjust, 1, i, 0);
            tx.CreatedAt = baseTime.AddSeconds(i);
            context.StockTransactions.Add(tx);
        }
        await context.SaveChangesAsync();

        var handler = new GetStockTransactionsHandler(context);
        var result = (await handler.Handle(new GetStockTransactionsQuery(null), CancellationToken.None)).ToList();

        result.Should().HaveCount(200, "query giới hạn Take(200) để tránh trả về lịch sử vô hạn");
        result.First().QuantityOnHandAfter.Should().Be(totalRows - 1, "phải sắp xếp mới nhất trước (OrderByDescending CreatedAt)");
    }
}
