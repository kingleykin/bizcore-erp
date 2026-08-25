using MediatR;
using Microsoft.EntityFrameworkCore;
using Product.API.Application.DTOs;
using Product.API.Infrastructure.Data;

namespace Product.API.Application.Queries;

// 1. Get All Products
public record GetProductsQuery(bool? IsActive = null) : IRequest<IEnumerable<ProductResponseDto>>;

public class GetProductsHandler : IRequestHandler<GetProductsQuery, IEnumerable<ProductResponseDto>>
{
    private readonly AppDbContext _db;

    public GetProductsHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<ProductResponseDto>> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var query = _db.Products.AsNoTracking().AsQueryable();

        if (request.IsActive.HasValue)
            query = query.Where(p => p.IsActive == request.IsActive.Value);

        var entities = await query.OrderBy(p => p.Name).ToListAsync(ct);
        return entities.Select(e => e.ToDto());
    }
}

// 2. Get Product By Id
public record GetProductByIdQuery(Guid Id) : IRequest<ProductResponseDto?>;

public class GetProductByIdHandler : IRequestHandler<GetProductByIdQuery, ProductResponseDto?>
{
    private readonly AppDbContext _db;

    public GetProductByIdHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ProductResponseDto?> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        var entity = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        return entity?.ToDto();
    }
}
