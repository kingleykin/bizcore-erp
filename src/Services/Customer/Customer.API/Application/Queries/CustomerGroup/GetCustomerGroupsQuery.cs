using MediatR;
using Microsoft.EntityFrameworkCore;
using Customer.API.Application.DTOs;
using Customer.API.Infrastructure.Data;

namespace Customer.API.Application.Queries.CustomerGroup;

public record GetCustomerGroupsQuery : IRequest<IEnumerable<CustomerGroupResponseDto>>;

public class GetCustomerGroupsHandler : IRequestHandler<GetCustomerGroupsQuery, IEnumerable<CustomerGroupResponseDto>>
{
    private readonly CustomerDbContext _db;

    public GetCustomerGroupsHandler(CustomerDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<CustomerGroupResponseDto>> Handle(GetCustomerGroupsQuery request, CancellationToken ct)
    {
        var entities = await _db.CustomerGroups.AsNoTracking().ToListAsync(ct);
        return entities.Select(e => new CustomerGroupResponseDto(
            e.Id,
            e.NameCustomerGroup,
            e.Code,
            e.Description,
            e.Status,
            e.CreatedAt
        ));
    }
}

public record GetCustomerGroupByIdQuery(Guid Id) : IRequest<CustomerGroupResponseDto?>;

public class GetCustomerGroupByIdHandler : IRequestHandler<GetCustomerGroupByIdQuery, CustomerGroupResponseDto?>
{
    private readonly CustomerDbContext _db;

    public GetCustomerGroupByIdHandler(CustomerDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerGroupResponseDto?> Handle(GetCustomerGroupByIdQuery request, CancellationToken ct)
    {
        var entity = await _db.CustomerGroups.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (entity == null) return null;

        return new CustomerGroupResponseDto(
            entity.Id,
            entity.NameCustomerGroup,
            entity.Code,
            entity.Description,
            entity.Status,
            entity.CreatedAt
        );
    }
}
