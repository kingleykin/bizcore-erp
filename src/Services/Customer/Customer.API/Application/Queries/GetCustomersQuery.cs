using MediatR;
using Microsoft.EntityFrameworkCore;
using Customer.API.Application.DTOs;
using Customer.API.Domain.Entities;
using Customer.API.Infrastructure.Data;

namespace Customer.API.Application.Queries;

// 1. Get All Customers
public record GetCustomersQuery : IRequest<IEnumerable<CustomerResponseDto>>;

public class GetCustomersHandler : IRequestHandler<GetCustomersQuery, IEnumerable<CustomerResponseDto>>
{
    private readonly CustomerDbContext _db;

    public GetCustomersHandler(CustomerDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<CustomerResponseDto>> Handle(GetCustomersQuery request, CancellationToken ct)
    {
        var entities = await _db.Customers.AsNoTracking().ToListAsync(ct);
        return entities.Select(e => new CustomerResponseDto(
            e.Id,
            e.FirstName,
            e.LastName,
            e.Email,
            e.Phone,
            e.Address,
            e.Status,
            e.CustomerPoint,
            e.SoTienTrongTaiKhoan,
            e.SoTienTongHoaDon,
            e.CustomerGroupId,
            e.CreatedAt,
            e.Version
        ));
    }
}

// 2. Get Customer By Id
public record GetCustomerByIdQuery(Guid Id) : IRequest<CustomerResponseDto?>;

public class GetCustomerByIdHandler : IRequestHandler<GetCustomerByIdQuery, CustomerResponseDto?>
{
    private readonly CustomerDbContext _db;

    public GetCustomerByIdHandler(CustomerDbContext db)
    {
        _db = db;
    }

    public async Task<CustomerResponseDto?> Handle(GetCustomerByIdQuery request, CancellationToken ct)
    {
        var entity = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (entity == null) return null;
        return new CustomerResponseDto(
            entity.Id,
            entity.FirstName,
            entity.LastName,
            entity.Email,
            entity.Phone,
            entity.Address,
            entity.Status,
            entity.CustomerPoint,
            entity.SoTienTrongTaiKhoan,
            entity.SoTienTongHoaDon,
            entity.CustomerGroupId,
            entity.CreatedAt,
            entity.Version
        );
    }
}
