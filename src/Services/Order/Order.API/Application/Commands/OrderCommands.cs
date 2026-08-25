using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Order.API.Application.Clients;
using Order.API.Application.DTOs;
using Order.API.Infrastructure.Data;

namespace Order.API.Application.Commands;

// 1. Create Order
// Không đánh dấu ITransactionalCommand: bước này chỉ resolve dữ liệu qua HTTP (Customer/Product
// service), chưa ghi DB. Nếu để TransactionBehavior bọc luôn bước này, transaction SQL sẽ bị giữ mở
// suốt thời gian chờ 2 service ngoài phản hồi — không cần thiết và tốn connection pool.
// Việc ghi DB thực sự được giao cho PersistOrderCommand (có ITransactionalCommand) bên dưới.
public record CreateOrderCommand(CreateOrderRequest Request) : IRequest<OrderResponseDto>;

public class CreateOrderHandler : IRequestHandler<CreateOrderCommand, OrderResponseDto>
{
    private readonly ICustomerServiceClient _customerClient;
    private readonly IProductServiceClient _productClient;
    private readonly IMediator _mediator;

    public CreateOrderHandler(
        ICustomerServiceClient customerClient,
        IProductServiceClient productClient,
        IMediator mediator)
    {
        _customerClient = customerClient;
        _productClient = productClient;
        _mediator = mediator;
    }

    public async Task<OrderResponseDto> Handle(CreateOrderCommand command, CancellationToken ct)
    {
        var customer = await _customerClient.GetCustomerAsync(command.Request.CustomerId, ct);
        if (customer == null)
            throw new NotFoundException(
                ErrorCodes.Order.CustomerNotFound,
                "Không tìm thấy khách hàng.",
                new { customerId = command.Request.CustomerId });

        var resolvedItems = new List<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)>();
        foreach (var item in command.Request.Items)
        {
            var product = await _productClient.GetProductAsync(item.ProductId, ct);
            if (product == null)
                throw new NotFoundException(
                    ErrorCodes.Order.ProductNotFound,
                    "Không tìm thấy sản phẩm.",
                    new { productId = item.ProductId });

            resolvedItems.Add((product.Id, product.Name, item.Quantity, item.UnitPrice));
        }

        return await _mediator.Send(
            new PersistOrderCommand(customer.Id, customer.Name, command.Request.Note, resolvedItems), ct);
    }
}

// 1b. Persist Order — bước ghi DB thực sự, gói trong transaction ngắn (không có I/O ngoài DB).
public record PersistOrderCommand(
    Guid CustomerId,
    string CustomerName,
    string? Note,
    List<(Guid ProductId, string ProductName, int Quantity, decimal UnitPrice)> Items
) : IRequest<OrderResponseDto>, ITransactionalCommand;

public class PersistOrderHandler : IRequestHandler<PersistOrderCommand, OrderResponseDto>
{
    private readonly AppDbContext _db;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<PersistOrderHandler> _logger;

    public PersistOrderHandler(AppDbContext db, IAuditPublisher audit, ILogger<PersistOrderHandler> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<OrderResponseDto> Handle(PersistOrderCommand command, CancellationToken ct)
    {
        var order = Domain.Entities.Order.Create(
            command.CustomerId,
            command.CustomerName,
            command.Note,
            command.Items);

        _db.Orders.Add(order);

        await _audit.PublishAsync(
            AuditActions.Order.Created,
            entityType: "Order",
            entityId: order.Id.ToString(),
            after: new { order.Id, order.OrderNumber, order.CustomerId, order.TotalAmount, order.Status },
            category: AuditCategory.Business,
            classification: DataClassification.Financial,
            ct: ct);

        _logger.LogInformation("OrderCreated OrderId={OrderId}, OrderNumber={OrderNumber}", order.Id, order.OrderNumber);

        return order.ToDto();
    }
}

// 2. Confirm Order
public record ConfirmOrderCommand(Guid Id) : IRequest<OrderResponseDto>, ITransactionalCommand;

public class ConfirmOrderHandler : IRequestHandler<ConfirmOrderCommand, OrderResponseDto>
{
    private readonly AppDbContext _db;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<ConfirmOrderHandler> _logger;

    public ConfirmOrderHandler(AppDbContext db, IAuditPublisher audit, ILogger<ConfirmOrderHandler> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<OrderResponseDto> Handle(ConfirmOrderCommand command, CancellationToken ct)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == command.Id, ct);
        if (order == null)
            throw new NotFoundException(ErrorCodes.Order.NotFound, "Không tìm thấy đơn hàng.", new { id = command.Id });

        order.Confirm();

        await _audit.PublishAsync(
            AuditActions.Order.Confirmed,
            entityType: "Order",
            entityId: order.Id.ToString(),
            after: new { order.Id, order.Status },
            category: AuditCategory.Business,
            classification: DataClassification.Financial,
            ct: ct);

        _logger.LogInformation("OrderConfirmed OrderId={OrderId}", order.Id);

        return order.ToDto();
    }
}

// 3. Cancel Order
public record CancelOrderCommand(Guid Id, string Reason) : IRequest<OrderResponseDto>, ITransactionalCommand;

public class CancelOrderHandler : IRequestHandler<CancelOrderCommand, OrderResponseDto>
{
    private readonly AppDbContext _db;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<CancelOrderHandler> _logger;

    public CancelOrderHandler(AppDbContext db, IAuditPublisher audit, ILogger<CancelOrderHandler> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task<OrderResponseDto> Handle(CancelOrderCommand command, CancellationToken ct)
    {
        var order = await _db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == command.Id, ct);
        if (order == null)
            throw new NotFoundException(ErrorCodes.Order.NotFound, "Không tìm thấy đơn hàng.", new { id = command.Id });

        order.Cancel(command.Reason);

        await _audit.PublishAsync(
            AuditActions.Order.Cancelled,
            entityType: "Order",
            entityId: order.Id.ToString(),
            after: new { order.Id, order.Status, order.CancelReason },
            category: AuditCategory.Business,
            classification: DataClassification.Financial,
            ct: ct);

        _logger.LogInformation("OrderCancelled OrderId={OrderId}, Reason={Reason}", order.Id, order.CancelReason);

        return order.ToDto();
    }
}
