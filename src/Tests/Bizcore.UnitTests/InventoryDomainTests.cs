using Bizcore.BuildingBlocks.Exceptions;
using FluentAssertions;
using Inventory.API.Domain.Entities;

namespace Bizcore.UnitTests;

public class InventoryDomainTests
{
    private static readonly Guid ProductId = Guid.NewGuid();

    [Fact]
    public void Stock_Create_WithValidData_SetsFieldsAndZeroesReserved()
    {
        var stock = Stock.Create(ProductId, "Bàn phím cơ", initialOnHand: 50);

        stock.ProductId.Should().Be(ProductId);
        stock.ProductName.Should().Be("Bàn phím cơ");
        stock.QuantityOnHand.Should().Be(50);
        stock.QuantityReserved.Should().Be(0);
        stock.AvailableQuantity.Should().Be(50);
    }

    [Fact]
    public void Stock_Create_WithEmptyProductId_Throws()
    {
        var act = () => Stock.Create(Guid.Empty, "Sản phẩm", 10);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Stock_Create_WithBlankProductName_Throws()
    {
        var act = () => Stock.Create(ProductId, "   ", 10);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Stock_Create_WithNegativeInitialOnHand_Throws()
    {
        var act = () => Stock.Create(ProductId, "Sản phẩm", -1);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Stock_Reserve_IncreasesReserved_LeavesOnHandUnchanged()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 50);

        stock.Reserve(20);

        stock.QuantityOnHand.Should().Be(50);
        stock.QuantityReserved.Should().Be(20);
        stock.AvailableQuantity.Should().Be(30);
    }

    [Fact]
    public void Stock_Reserve_BeyondOnHand_ThrowsInsufficientStock_NoOversell()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 5);

        var act = () => stock.Reserve(8);

        act.Should().Throw<DomainException>();
        stock.QuantityReserved.Should().Be(0, "Reserve thất bại thì không được thay đổi trạng thái");
        stock.AvailableQuantity.Should().Be(5);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Stock_Reserve_WithNonPositiveQuantity_Throws(int quantity)
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 10);
        var act = () => stock.Reserve(quantity);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Stock_Commit_DecreasesBothOnHandAndReserved()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 50);
        stock.Reserve(20);

        stock.Commit(20);

        stock.QuantityOnHand.Should().Be(30);
        stock.QuantityReserved.Should().Be(0);
        stock.AvailableQuantity.Should().Be(30);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Stock_Commit_WithNonPositiveQuantity_Throws(int quantity)
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 10);
        var act = () => stock.Commit(quantity);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Stock_Uncommit_IncreasesBothOnHandAndReserved_ExactlyReversesCommit()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 50);
        stock.Reserve(20);
        stock.Commit(20);

        stock.Uncommit(20);

        stock.QuantityOnHand.Should().Be(50, "Uncommit phải trả OnHand về đúng trước lúc Commit");
        stock.QuantityReserved.Should().Be(20, "Uncommit phải trả Reserved về đúng trước lúc Commit (đơn coi như Pending trở lại)");
        stock.AvailableQuantity.Should().Be(30);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Stock_Uncommit_WithNonPositiveQuantity_Throws(int quantity)
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 10);
        var act = () => stock.Uncommit(quantity);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Stock_Release_DecreasesReserved_LeavesOnHandUnchanged()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 50);
        stock.Reserve(20);

        stock.Release(20);

        stock.QuantityOnHand.Should().Be(50);
        stock.QuantityReserved.Should().Be(0);
    }

    [Fact]
    public void Stock_Release_MoreThanReserved_FloorsAtZero_DoesNotGoNegative()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 50);
        stock.Reserve(5);

        stock.Release(20);

        stock.QuantityReserved.Should().Be(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Stock_Release_WithNonPositiveQuantity_Throws(int quantity)
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 10);
        var act = () => stock.Release(quantity);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Stock_AdjustOnHand_SetsNewValue()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 10);

        stock.AdjustOnHand(100);

        stock.QuantityOnHand.Should().Be(100);
    }

    [Fact]
    public void Stock_AdjustOnHand_WithNegativeValue_Throws()
    {
        var stock = Stock.Create(ProductId, "Sản phẩm", 10);
        var act = () => stock.AdjustOnHand(-1);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void StockTransaction_Create_SetsAllFields()
    {
        var orderId = Guid.NewGuid();

        var tx = StockTransaction.Create(
            ProductId,
            "Sản phẩm",
            StockTransactionType.Reserve,
            quantity: 5,
            quantityOnHandAfter: 50,
            quantityReservedAfter: 5,
            relatedOrderId: orderId,
            note: "ghi chú");

        tx.ProductId.Should().Be(ProductId);
        tx.ProductName.Should().Be("Sản phẩm");
        tx.Type.Should().Be(StockTransactionType.Reserve);
        tx.Quantity.Should().Be(5);
        tx.QuantityOnHandAfter.Should().Be(50);
        tx.QuantityReservedAfter.Should().Be(5);
        tx.RelatedOrderId.Should().Be(orderId);
        tx.Note.Should().Be("ghi chú");
    }

    [Fact]
    public void StockTransaction_Create_WithoutOptionalArgs_DefaultsToNull()
    {
        var tx = StockTransaction.Create(
            ProductId,
            "Sản phẩm",
            StockTransactionType.Adjust,
            quantity: -3,
            quantityOnHandAfter: 7,
            quantityReservedAfter: 0);

        tx.RelatedOrderId.Should().BeNull();
        tx.Note.Should().BeNull();
        tx.Quantity.Should().Be(-3);
    }
}
