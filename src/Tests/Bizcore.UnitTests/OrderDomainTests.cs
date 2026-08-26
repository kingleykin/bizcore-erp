using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Exceptions;
using FluentAssertions;
using OrderEntity = Order.API.Domain.Entities.Order;
using OrderItemEntity = Order.API.Domain.Entities.OrderItem;

namespace Bizcore.UnitTests;

public class OrderDomainTests
{
    private static (Guid ProductId, string ProductName, int Quantity, decimal UnitPrice) DefaultItem(
        int quantity = 2, decimal unitPrice = 10m) => (Guid.NewGuid(), "Sản phẩm", quantity, unitPrice);

    // ---------- Order.Create ----------

    [Fact]
    public void Create_WithValidData_SetsFieldsAndPendingStatus()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), "  Khách A  ", "ghi chú", [DefaultItem()]);

        order.CustomerName.Should().Be("Khách A", "tên khách hàng phải được Trim()");
        order.Status.Should().Be(OrderStatus.Pending);
        order.CancelReason.Should().BeNull();
        order.Items.Should().ContainSingle();
    }

    [Fact]
    public void Create_GeneratesOrderNumber_WithOrdPrefixAndTodayDate()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), "Khách", null, [DefaultItem()]);

        order.OrderNumber.Should().StartWith("ORD");
        order.OrderNumber.Should().Contain(DateTime.UtcNow.ToString("yyyyMMdd"));
    }

    [Fact]
    public void Create_ComputesTotalAmount_AsSumOfQuantityTimesUnitPrice()
    {
        var items = new List<(Guid, string, int, decimal)>
        {
            (Guid.NewGuid(), "A", 2, 10m),
            (Guid.NewGuid(), "B", 3, 5m)
        };

        var order = OrderEntity.Create(Guid.NewGuid(), "Khách", null, items);

        order.TotalAmount.Should().Be(35m); // 2*10 + 3*5
    }

    [Fact]
    public void Create_WithEmptyCustomerId_Throws()
    {
        var act = () => OrderEntity.Create(Guid.Empty, "Khách", null, [DefaultItem()]);
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankCustomerName_Throws(string name)
    {
        var act = () => OrderEntity.Create(Guid.NewGuid(), name, null, [DefaultItem()]);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Create_WithNoItems_Throws_WithEmptyItemsErrorCode()
    {
        var act = () => OrderEntity.Create(Guid.NewGuid(), "Khách", null, []);

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be(ErrorCodes.Order.EmptyItems);
    }

    // ---------- Order.Confirm ----------

    [Fact]
    public void Confirm_FromPending_TransitionsToConfirmed()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), "Khách", null, [DefaultItem()]);

        order.Confirm();

        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void Confirm_WhenAlreadyConfirmed_IsIdempotent_DoesNotThrow()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), "Khách", null, [DefaultItem()]);
        order.Confirm();

        var act = () => order.Confirm();

        act.Should().NotThrow();
        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void Confirm_WhenCancelled_Throws_WithInvalidStatusErrorCode()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), "Khách", null, [DefaultItem()]);
        order.Cancel("khách hủy");

        var act = () => order.Confirm();

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be(ErrorCodes.Order.InvalidStatus);
    }

    // ---------- Order.Cancel ----------

    [Fact]
    public void Cancel_FromPending_SetsCancelledStatus_AndTrimmedReason()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), "Khách", null, [DefaultItem()]);

        order.Cancel("  Khách đổi ý  ");

        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelReason.Should().Be("Khách đổi ý");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cancel_WithBlankReason_Throws(string reason)
    {
        var order = OrderEntity.Create(Guid.NewGuid(), "Khách", null, [DefaultItem()]);

        var act = () => order.Cancel(reason);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Cancel_WhenAlreadyConfirmed_Throws_WithInvalidStatusErrorCode()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), "Khách", null, [DefaultItem()]);
        order.Confirm();

        var act = () => order.Cancel("lý do");

        act.Should().Throw<DomainException>()
            .Which.Code.Should().Be(ErrorCodes.Order.InvalidStatus);
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_Throws()
    {
        var order = OrderEntity.Create(Guid.NewGuid(), "Khách", null, [DefaultItem()]);
        order.Cancel("lần 1");

        var act = () => order.Cancel("lần 2");

        act.Should().Throw<DomainException>();
    }

    // ---------- OrderItem.Create ----------

    [Fact]
    public void OrderItem_Create_WithValidData_ComputesLineTotal()
    {
        var item = OrderItemEntity.Create(Guid.NewGuid(), Guid.NewGuid(), "Sản phẩm", 3, 15m);

        item.LineTotal.Should().Be(45m);
    }

    [Fact]
    public void OrderItem_Create_WithEmptyProductId_Throws()
    {
        var act = () => OrderItemEntity.Create(Guid.NewGuid(), Guid.Empty, "Sản phẩm", 1, 10m);
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void OrderItem_Create_WithNonPositiveQuantity_Throws(int quantity)
    {
        var act = () => OrderItemEntity.Create(Guid.NewGuid(), Guid.NewGuid(), "Sản phẩm", quantity, 10m);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void OrderItem_Create_WithNegativeUnitPrice_Throws()
    {
        var act = () => OrderItemEntity.Create(Guid.NewGuid(), Guid.NewGuid(), "Sản phẩm", 1, -0.01m);
        act.Should().Throw<DomainException>();
    }
}
