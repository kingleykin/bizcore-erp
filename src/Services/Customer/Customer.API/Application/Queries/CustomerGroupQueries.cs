using Customer.API.Application.DTOs;
using Customer.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Customer.API.Application.Queries;

// 1. Get All CustomerGroups
public record GetCustomerGroupsQuery : IRequest<IEnumerable<CustomerGroupResponseDto>>;

public class GetCustomerGroupsHandler : IRequestHandler<GetCustomerGroupsQuery, IEnumerable<CustomerGroupResponseDto>>
{
    private readonly AppDbContext _db;

    public GetCustomerGroupsHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<CustomerGroupResponseDto>> Handle(GetCustomerGroupsQuery request, CancellationToken ct)
    {
        var entities = await _db.CustomerGroups.AsNoTracking().OrderBy(g => g.Code).ToListAsync(ct);
        return entities.Select(e => e.ToDto());
    }
}

// 2. Get CustomerGroup By Id
public record GetCustomerGroupByIdQuery(Guid Id) : IRequest<CustomerGroupResponseDto?>;

public class GetCustomerGroupByIdHandler : IRequestHandler<GetCustomerGroupByIdQuery, CustomerGroupResponseDto?>
{
    private readonly AppDbContext _db;

    public GetCustomerGroupByIdHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerGroupResponseDto?> Handle(GetCustomerGroupByIdQuery request, CancellationToken ct)
    {
        var entity = await _db.CustomerGroups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        return entity?.ToDto();
    }
}
