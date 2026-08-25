using MediatR;
using Microsoft.EntityFrameworkCore;
using Order.API.Application.DTOs;
using Order.API.Infrastructure.Data;

namespace Order.API.Application.Queries;

// 1. Get All Orders
public record GetOrdersQuery(Guid? CustomerId = null) : IRequest<IEnumerable<OrderResponseDto>>;

public class GetOrdersHandler : IRequestHandler<GetOrdersQuery, IEnumerable<OrderResponseDto>>
{
    private readonly AppDbContext _db;

    public GetOrdersHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<OrderResponseDto>> Handle(GetOrdersQuery request, CancellationToken ct)
    {
        var query = _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .AsQueryable();

        if (request.CustomerId.HasValue)
            query = query.Where(o => o.CustomerId == request.CustomerId.Value);

        var entities = await query.OrderByDescending(o => o.OrderDate).ToListAsync(ct);
        return entities.Select(e => e.ToDto());
    }
}

// 2. Get Order By Id
public record GetOrderByIdQuery(Guid Id) : IRequest<OrderResponseDto?>;

public class GetOrderByIdHandler : IRequestHandler<GetOrderByIdQuery, OrderResponseDto?>
{
    private readonly AppDbContext _db;

    public GetOrderByIdHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<OrderResponseDto?> Handle(GetOrderByIdQuery request, CancellationToken ct)
    {
        var entity = await _db.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        return entity?.ToDto();
    }
}
