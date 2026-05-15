using MediatR;
using Microsoft.EntityFrameworkCore;
using Invoice.API.Application.DTOs;
using Invoice.API.Infrastructure.Data;

namespace Invoice.API.Application.Queries;

// 1. Get All Invoices
public record GetInvoicesQuery : IRequest<IEnumerable<InvoiceResponseDto>>;

public class GetInvoicesHandler : IRequestHandler<GetInvoicesQuery, IEnumerable<InvoiceResponseDto>>
{
    private readonly AppDbContext _db;

    public GetInvoicesHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<InvoiceResponseDto>> Handle(GetInvoicesQuery request, CancellationToken ct)
    {
        var entities = await _db.Invoices.AsNoTracking().ToListAsync(ct);
        return entities.Select(e => e.ToDto());
    }
}

// 2. Get Invoice By Id
public record GetInvoiceByIdQuery(Guid Id) : IRequest<InvoiceResponseDto?>;

public class GetInvoiceByIdHandler : IRequestHandler<GetInvoiceByIdQuery, InvoiceResponseDto?>
{
    private readonly AppDbContext _db;

    public GetInvoiceByIdHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<InvoiceResponseDto?> Handle(GetInvoiceByIdQuery request, CancellationToken ct)
    {
        var entity = await _db.Invoices.AsNoTracking().FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        return entity?.ToDto();
    }
}
