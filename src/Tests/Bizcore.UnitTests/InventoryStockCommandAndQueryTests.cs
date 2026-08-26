using Bizcore.BuildingBlocks.Audit;
using FluentAssertions;
using Inventory.API.Application.Commands;
using Inventory.API.Application.DTOs;
using Inventory.API.Application.Queries;
using Inventory.API.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Bizcore.UnitTests;

public class InventoryStockCommandAndQueryTests
{
    private static AdjustStockHandler BuildHandler(Inventory.API.Infrastructure.Data.AppDbContext context, Mock<IAuditPublisher>? auditMock = null)
    {
        auditMock ??= new Mock<IAuditPublisher>();
        return new AdjustStockHandler(context, auditMock.Object, NullLogger<AdjustStockHandler>.Instance);
    }

    [Fact]
    public async Task AdjustStockHandler_WhenProductHasNoStockYet_CreatesStock_LogsPositiveAdjust()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        var handler = BuildHandler(context);

        var result = await handler.Handle(
            new AdjustStockCommand(productId, new AdjustStockRequest("Sản phẩm mới", 40)),
            CancellationToken.None);

        // Handler không tự SaveChanges (TransactionBehavior/IUnitOfWork.CommitAsync làm việc đó
        // trong pipeline thật) — mô phỏng lại bước đó để assert được trạng thái đã lưu DB.
        await context.SaveChangesAsync();

        result.QuantityOnHand.Should().Be(40);
        context.Stocks.Single(s => s.ProductId == productId).QuantityOnHand.Should().Be(40);

        var tx = context.StockTransactions.Single();
        tx.Type.Should().Be(StockTransactionType.Adjust);
        tx.Quantity.Should().Be(40, "tăng từ 0 lên 40 nên delta là +40");
    }

    [Fact]
    public async Task AdjustStockHandler_WhenIncreasingExistingStock_LogsPositiveDelta()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        context.Stocks.Add(Stock.Create(productId, "Sản phẩm", 10));
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        await handler.Handle(new AdjustStockCommand(productId, new AdjustStockRequest("Sản phẩm", 25)), CancellationToken.None);
        await context.SaveChangesAsync();

        context.Stocks.Single(s => s.ProductId == productId).QuantityOnHand.Should().Be(25);
        context.StockTransactions.Single().Quantity.Should().Be(15);
    }

    [Fact]
    public async Task AdjustStockHandler_WhenDecreasingExistingStock_LogsNegativeDelta()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        context.Stocks.Add(Stock.Create(productId, "Sản phẩm", 30));
        await context.SaveChangesAsync();

        var handler = BuildHandler(context);
        await handler.Handle(new AdjustStockCommand(productId, new AdjustStockRequest("Sản phẩm", 10)), CancellationToken.None);
        await context.SaveChangesAsync();

        context.Stocks.Single(s => s.ProductId == productId).QuantityOnHand.Should().Be(10);
        context.StockTransactions.Single().Quantity.Should().Be(-20);
    }

    [Fact]
    public async Task GetStocksQuery_ReturnsAllStocks_OrderedByProductName()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        context.Stocks.Add(Stock.Create(Guid.NewGuid(), "Zebra", 1));
        context.Stocks.Add(Stock.Create(Guid.NewGuid(), "Apple", 1));
        await context.SaveChangesAsync();

        var handler = new GetStocksHandler(context);
        var result = (await handler.Handle(new GetStocksQuery(), CancellationToken.None)).ToList();

        result.Should().HaveCount(2);
        result[0].ProductName.Should().Be("Apple");
        result[1].ProductName.Should().Be("Zebra");
    }

    [Fact]
    public async Task GetStockByProductIdQuery_WhenExists_ReturnsDto()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productId = Guid.NewGuid();
        context.Stocks.Add(Stock.Create(productId, "Sản phẩm", 5));
        await context.SaveChangesAsync();

        var handler = new GetStockByProductIdHandler(context);
        var result = await handler.Handle(new GetStockByProductIdQuery(productId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.ProductId.Should().Be(productId);
    }

    [Fact]
    public async Task GetStockByProductIdQuery_WhenMissing_ReturnsNull()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var handler = new GetStockByProductIdHandler(context);
        var result = await handler.Handle(new GetStockByProductIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStockTransactionsQuery_WithoutProductFilter_ReturnsAllNewestFirst()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        context.StockTransactions.Add(StockTransaction.Create(productA, "A", StockTransactionType.Reserve, 1, 10, 1));
        context.StockTransactions.Add(StockTransaction.Create(productB, "B", StockTransactionType.Adjust, 5, 15, 0));
        await context.SaveChangesAsync();

        var handler = new GetStockTransactionsHandler(context);
        var result = (await handler.Handle(new GetStockTransactionsQuery(null), CancellationToken.None)).ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetStockTransactionsQuery_WithProductFilter_ReturnsOnlyThatProduct()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateInventoryDbContext(connection);

        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        context.StockTransactions.Add(StockTransaction.Create(productA, "A", StockTransactionType.Reserve, 1, 10, 1));
        context.StockTransactions.Add(StockTransaction.Create(productB, "B", StockTransactionType.Adjust, 5, 15, 0));
        await context.SaveChangesAsync();

        var handler = new GetStockTransactionsHandler(context);
        var result = (await handler.Handle(new GetStockTransactionsQuery(productA), CancellationToken.None)).ToList();

        result.Should().ContainSingle();
        result[0].ProductId.Should().Be(productA);
    }
}
