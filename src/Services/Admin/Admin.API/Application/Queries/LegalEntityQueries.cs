using Admin.API.Application.DTOs;
using Admin.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Queries;

// 1. Get All Legal Entities
public record GetLegalEntitiesQuery : IRequest<IEnumerable<LegalEntityResponse>>;

public class GetLegalEntitiesHandler : IRequestHandler<GetLegalEntitiesQuery, IEnumerable<LegalEntityResponse>>
{
    private readonly AdminDbContext _db;

    public GetLegalEntitiesHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<LegalEntityResponse>> Handle(GetLegalEntitiesQuery request, CancellationToken ct)
    {
        return await _db.LegalEntities
            .AsNoTracking()
            .OrderBy(e => e.Code)
            .Select(e => new LegalEntityResponse(
                e.Id, e.Code, e.Name, e.TaxCode, e.RegistrationNumber, e.Address, e.BaseCurrencyCode, (int)e.Status, e.CreatedAt, e.UpdatedAt))
            .ToListAsync(ct);
    }
}

// 2. Get Legal Entity By Id
public record GetLegalEntityByIdQuery(Guid Id) : IRequest<LegalEntityResponse?>;

public class GetLegalEntityByIdHandler : IRequestHandler<GetLegalEntityByIdQuery, LegalEntityResponse?>
{
    private readonly AdminDbContext _db;

    public GetLegalEntityByIdHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<LegalEntityResponse?> Handle(GetLegalEntityByIdQuery request, CancellationToken ct)
    {
        var e = await _db.LegalEntities
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (e == null) return null;

        return new LegalEntityResponse(
            e.Id, e.Code, e.Name, e.TaxCode, e.RegistrationNumber, e.Address, e.BaseCurrencyCode, (int)e.Status, e.CreatedAt, e.UpdatedAt);
    }
}
