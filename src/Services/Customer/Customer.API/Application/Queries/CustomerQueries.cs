using Customer.API.Application.DTOs;
using Customer.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Customer.API.Application.Queries;

// 1. Get All Customers
public record GetCustomersQuery(Guid? CustomerGroupId = null) : IRequest<IEnumerable<CustomerResponseDto>>;

public class GetCustomersHandler : IRequestHandler<GetCustomersQuery, IEnumerable<CustomerResponseDto>>
{
    private readonly AppDbContext _db;

    public GetCustomersHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<CustomerResponseDto>> Handle(GetCustomersQuery request, CancellationToken ct)
    {
        var query = _db.Customers
            .AsNoTracking()
            .Include(c => c.CustomerGroup)
            .AsQueryable();

        if (request.CustomerGroupId.HasValue)
            query = query.Where(c => c.CustomerGroupId == request.CustomerGroupId.Value);

        var entities = await query.OrderBy(c => c.Code).ToListAsync(ct);
        return entities.Select(e => e.ToDto());
    }
}

// 2. Get Customer By Id
public record GetCustomerByIdQuery(Guid Id) : IRequest<CustomerResponseDto?>;

public class GetCustomerByIdHandler : IRequestHandler<GetCustomerByIdQuery, CustomerResponseDto?>
{
    private readonly AppDbContext _db;

    public GetCustomerByIdHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerResponseDto?> Handle(GetCustomerByIdQuery request, CancellationToken ct)
    {
        var entity = await _db.Customers
            .AsNoTracking()
            .Include(c => c.CustomerGroup)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        return entity?.ToDto();
    }
}
