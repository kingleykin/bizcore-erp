using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Exceptions;
using MassTransit;
using Product.API.Application.DTOs;
using Product.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Product.API.Application.Commands;

// 1. Create Product
public record CreateProductCommand(CreateProductRequest Request) : IRequest<ProductResponseDto>, ITransactionalCommand;

public class CreateProductHandler : IRequestHandler<CreateProductCommand, ProductResponseDto>
{
    private readonly AppDbContext _db;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<CreateProductHandler> _logger;

    public CreateProductHandler(AppDbContext db, IPublishEndpoint publishEndpoint, IAuditPublisher audit, ILogger<CreateProductHandler> logger)
    {
        _db = db;
        _publishEndpoint = publishEndpoint;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ProductResponseDto> Handle(CreateProductCommand command, CancellationToken ct)
    {
        var product = Domain.Entities.Product.Create(
            command.Request.Name,
            command.Request.Unit,
            command.Request.Price,
            command.Request.Description);

        _db.Products.Add(product);

        // Inventory Service lắng nghe event này để tạo sẵn bản ghi tồn kho (OnHand=0) cho sản phẩm mới.
        await _publishEndpoint.Publish<IProductCreatedEvent>(new
        {
            product.Id,
            product.Name,
            product.CreatedAt
        }, ct);

        await _audit.PublishAsync(
            AuditActions.Product.Created,
            entityType: "Product",
            entityId: product.Id.ToString(),
            after: new { product.Id, product.Code, product.Name, product.Price },
            category: AuditCategory.Business,
            classification: DataClassification.Internal,
            ct: ct);

        _logger.LogInformation("ProductCreated ProductId={ProductId}, Code={Code}", product.Id, product.Code);

        return product.ToDto();
    }
}

// 2. Update Product
public record UpdateProductCommand(Guid Id, UpdateProductRequest Request) : IRequest<ProductResponseDto>, ITransactionalCommand;

public class UpdateProductHandler : IRequestHandler<UpdateProductCommand, ProductResponseDto>
{
    private readonly AppDbContext _db;
    private readonly ILogger<UpdateProductHandler> _logger;

    public UpdateProductHandler(AppDbContext db, ILogger<UpdateProductHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ProductResponseDto> Handle(UpdateProductCommand command, CancellationToken ct)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == command.Id, ct);
        if (product == null)
            throw new NotFoundException(ErrorCodes.Product.NotFound, "Không tìm thấy sản phẩm.", new { id = command.Id });

        product.Update(command.Request.Name, command.Request.Unit, command.Request.Price, command.Request.Description);

        _logger.LogInformation("ProductUpdated ProductId={ProductId}", product.Id);

        return product.ToDto();
    }
}

// 3. Deactivate Product
public record DeactivateProductCommand(Guid Id) : IRequest<bool>, ITransactionalCommand;

public class DeactivateProductHandler : IRequestHandler<DeactivateProductCommand, bool>
{
    private readonly AppDbContext _db;
    private readonly ILogger<DeactivateProductHandler> _logger;

    public DeactivateProductHandler(AppDbContext db, ILogger<DeactivateProductHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> Handle(DeactivateProductCommand command, CancellationToken ct)
    {
        var product = await _db.Products.FindAsync(new object[] { command.Id }, ct);
        if (product == null) return false;

        product.Deactivate();
        _logger.LogInformation("ProductDeactivated ProductId={ProductId}", product.Id);
        return true;
    }
}

// 4. Activate Product
public record ActivateProductCommand(Guid Id) : IRequest<bool>, ITransactionalCommand;

public class ActivateProductHandler : IRequestHandler<ActivateProductCommand, bool>
{
    private readonly AppDbContext _db;
    private readonly ILogger<ActivateProductHandler> _logger;

    public ActivateProductHandler(AppDbContext db, ILogger<ActivateProductHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<bool> Handle(ActivateProductCommand command, CancellationToken ct)
    {
        var product = await _db.Products.FindAsync(new object[] { command.Id }, ct);
        if (product == null) return false;

        product.Activate();
        _logger.LogInformation("ProductActivated ProductId={ProductId}", product.Id);
        return true;
    }
}
