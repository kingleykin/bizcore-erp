using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Exceptions;
using FluentAssertions;
using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Order.API.Application.Clients;
using Order.API.Application.Commands;
using Order.API.Application.DTOs;
using OrderEntity = Order.API.Domain.Entities.Order;

namespace Bizcore.UnitTests;

public class OrderEventPublishingTests
{
    private static Mock<IPublishEndpoint> BuildPublishMock() => new();

    // ---------- CreateOrderHandler (HTTP resolve step) ----------

    [Fact]
    public async Task CreateOrderHandler_WhenCustomerAndProductsResolve_SendsPersistOrderCommandWithResolvedData()
    {
        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        var customerClient = new Mock<ICustomerServiceClient>();
        customerClient
            .Setup(c => c.GetCustomerAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerInfo(customerId, "Khách A", true));

        var productClient = new Mock<IProductServiceClient>();
        productClient
            .Setup(p => p.GetProductAsync(productId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProductInfo(productId, "SP001", "Sản phẩm A", 50m, true));

        PersistOrderCommand? captured = null;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<PersistOrderCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<OrderResponseDto>, CancellationToken>((cmd, _) => captured = (PersistOrderCommand)cmd)
            .ReturnsAsync(new OrderResponseDto(Guid.NewGuid(), "ORD001", customerId, "Khách A", DateTime.UtcNow, null, 100m,
                Bizcore.BuildingBlocks.OrderStatus.Pending, null, [], DateTime.UtcNow, DateTime.UtcNow));

        var handler = new CreateOrderHandler(customerClient.Object, productClient.Object, mediator.Object);

        var request = new CreateOrderRequest(customerId, "ghi chú", [new CreateOrderItemRequest(productId, 2, 50m)]);
        await handler.Handle(new CreateOrderCommand(request), CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.CustomerId.Should().Be(customerId);
        captured.CustomerName.Should().Be("Khách A");
        captured.Items.Should().ContainSingle();
        captured.Items[0].ProductId.Should().Be(productId);
        captured.Items[0].ProductName.Should().Be("Sản phẩm A");
        captured.Items[0].Quantity.Should().Be(2);
    }

    [Fact]
    public async Task CreateOrderHandler_WhenCustomerMissing_ThrowsNotFoundException_DoesNotCallProductClient()
    {
        var customerClient = new Mock<ICustomerServiceClient>();
        customerClient
            .Setup(c => c.GetCustomerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CustomerInfo?)null);

        var productClient = new Mock<IProductServiceClient>(MockBehavior.Strict);
        var mediator = new Mock<IMediator>(MockBehavior.Strict);

        var handler = new CreateOrderHandler(customerClient.Object, productClient.Object, mediator.Object);
        var request = new CreateOrderRequest(Guid.NewGuid(), null, [new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m)]);

        var act = async () => await handler.Handle(new CreateOrderCommand(request), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CreateOrderHandler_WhenProductMissing_ThrowsNotFoundException()
    {
        var customerId = Guid.NewGuid();
        var customerClient = new Mock<ICustomerServiceClient>();
        customerClient
            .Setup(c => c.GetCustomerAsync(customerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CustomerInfo(customerId, "Khách A", true));

        var productClient = new Mock<IProductServiceClient>();
        productClient
            .Setup(p => p.GetProductAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProductInfo?)null);

        var mediator = new Mock<IMediator>(MockBehavior.Strict);

        var handler = new CreateOrderHandler(customerClient.Object, productClient.Object, mediator.Object);
        var request = new CreateOrderRequest(customerId, null, [new CreateOrderItemRequest(Guid.NewGuid(), 1, 10m)]);

        var act = async () => await handler.Handle(new CreateOrderCommand(request), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    // ---------- PersistOrderHandler (DB write + publish) ----------

    [Fact]
    public async Task PersistOrderHandler_Handle_PersistsOrder_AndPublishesOrderCreatedEvent_WithMatchingItems()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var publishMock = BuildPublishMock();
        OrderCreatedEvent? published = null;
        publishMock
            .Setup(p => p.Publish(It.IsAny<OrderCreatedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OrderCreatedEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        var handler = new PersistOrderHandler(context, publishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<PersistOrderHandler>.Instance);

        var customerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var command = new PersistOrderCommand(customerId, "Khách B", null,
            [(productId, "Sản phẩm B", 3, 20m)]);

        var result = await handler.Handle(command, CancellationToken.None);

        // Handler không tự SaveChanges (TransactionBehavior/IUnitOfWork.CommitAsync làm việc đó
        // trong pipeline thật) — mô phỏng lại bước đó để assert được trạng thái đã lưu DB.
        await context.SaveChangesAsync();

        context.Orders.Should().ContainSingle();
        result.CustomerId.Should().Be(customerId);
        result.TotalAmount.Should().Be(60m);

        published.Should().NotBeNull("PersistOrderHandler phải publish OrderCreatedEvent để Inventory Service giữ chỗ tồn kho");
        published!.CustomerId.Should().Be(customerId);
        published.Items.Should().ContainSingle();
        published.Items.Single().ProductId.Should().Be(productId);
        published.Items.Single().Quantity.Should().Be(3);
    }

    // ---------- ConfirmOrderHandler ----------

    [Fact]
    public async Task ConfirmOrderHandler_WhenPending_ConfirmsAndPublishesOrderConfirmedEvent()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var productId = Guid.NewGuid();
        var order = OrderEntity.Create(Guid.NewGuid(), "Khách C", null, [(productId, "Sản phẩm C", 4, 15m)]);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var publishMock = BuildPublishMock();
        OrderConfirmedEvent? published = null;
        publishMock
            .Setup(p => p.Publish(It.IsAny<OrderConfirmedEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OrderConfirmedEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        var handler = new ConfirmOrderHandler(context, publishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<ConfirmOrderHandler>.Instance);
        var result = await handler.Handle(new ConfirmOrderCommand(order.Id), CancellationToken.None);

        result.Status.Should().Be(Bizcore.BuildingBlocks.OrderStatus.Confirmed);
        published.Should().NotBeNull();
        published!.Id.Should().Be(order.Id);
        published.Items.Single().ProductId.Should().Be(productId);
        published.Items.Single().Quantity.Should().Be(4);
    }

    [Fact]
    public async Task ConfirmOrderHandler_WhenAlreadyConfirmed_IsIdempotent_DoesNotPublishAgain()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var order = OrderEntity.Create(Guid.NewGuid(), "Khách D", null, [(Guid.NewGuid(), "SP", 1, 10m)]);
        order.Confirm();
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);

        var handler = new ConfirmOrderHandler(context, publishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<ConfirmOrderHandler>.Instance);
        var result = await handler.Handle(new ConfirmOrderCommand(order.Id), CancellationToken.None);

        result.Status.Should().Be(Bizcore.BuildingBlocks.OrderStatus.Confirmed);
        publishMock.Verify(p => p.Publish(It.IsAny<OrderConfirmedEvent>(), It.IsAny<CancellationToken>()), Times.Never,
            "Confirm lần 2 trên đơn đã Confirmed không được publish lại — tránh trừ kho lặp lại ở Inventory Service");
    }

    [Fact]
    public async Task ConfirmOrderHandler_WhenOrderMissing_ThrowsNotFoundException()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var handler = new ConfirmOrderHandler(context, Mock.Of<IPublishEndpoint>(), Mock.Of<IAuditPublisher>(), NullLogger<ConfirmOrderHandler>.Instance);

        var act = async () => await handler.Handle(new ConfirmOrderCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ConfirmOrderHandler_WhenOrderCancelled_ThrowsDomainException()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var order = OrderEntity.Create(Guid.NewGuid(), "Khách E", null, [(Guid.NewGuid(), "SP", 1, 10m)]);
        order.Cancel("khách hủy");
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var handler = new ConfirmOrderHandler(context, Mock.Of<IPublishEndpoint>(), Mock.Of<IAuditPublisher>(), NullLogger<ConfirmOrderHandler>.Instance);

        var act = async () => await handler.Handle(new ConfirmOrderCommand(order.Id), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }

    // ---------- CancelOrderHandler ----------

    [Fact]
    public async Task CancelOrderHandler_WhenPending_CancelsAndPublishesOrderCancelledEvent_WithReason()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var productId = Guid.NewGuid();
        var order = OrderEntity.Create(Guid.NewGuid(), "Khách F", null, [(productId, "Sản phẩm F", 2, 30m)]);
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var publishMock = BuildPublishMock();
        OrderCancelledEvent? published = null;
        publishMock
            .Setup(p => p.Publish(It.IsAny<OrderCancelledEvent>(), It.IsAny<CancellationToken>()))
            .Callback<OrderCancelledEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        var handler = new CancelOrderHandler(context, publishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<CancelOrderHandler>.Instance);
        var result = await handler.Handle(new CancelOrderCommand(order.Id, "Khách đổi ý"), CancellationToken.None);

        result.Status.Should().Be(Bizcore.BuildingBlocks.OrderStatus.Cancelled);
        published.Should().NotBeNull();
        published!.Reason.Should().Be("Khách đổi ý");
        published.Items.Single().ProductId.Should().Be(productId);
        published.Items.Single().Quantity.Should().Be(2);
    }

    [Fact]
    public async Task CancelOrderHandler_WhenOrderMissing_ThrowsNotFoundException()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var handler = new CancelOrderHandler(context, Mock.Of<IPublishEndpoint>(), Mock.Of<IAuditPublisher>(), NullLogger<CancelOrderHandler>.Instance);

        var act = async () => await handler.Handle(new CancelOrderCommand(Guid.NewGuid(), "lý do"), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task CancelOrderHandler_WhenOrderAlreadyConfirmed_ThrowsDomainException_DoesNotPublish()
    {
        using var connection = TestDbContextFactory.CreateOpenConnection();
        using var context = TestDbContextFactory.CreateOrderDbContext(connection);

        var order = OrderEntity.Create(Guid.NewGuid(), "Khách G", null, [(Guid.NewGuid(), "SP", 1, 10m)]);
        order.Confirm();
        context.Orders.Add(order);
        await context.SaveChangesAsync();

        var publishMock = new Mock<IPublishEndpoint>(MockBehavior.Strict);
        var handler = new CancelOrderHandler(context, publishMock.Object, Mock.Of<IAuditPublisher>(), NullLogger<CancelOrderHandler>.Instance);

        var act = async () => await handler.Handle(new CancelOrderCommand(order.Id, "lý do"), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
    }
}
