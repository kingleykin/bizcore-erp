using Admin.API.Application.DTOs;
using Admin.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Queries;

// 1. Get All Cost Centers
public record GetCostCentersQuery(Guid? LegalEntityId = null) : IRequest<IEnumerable<CostCenterResponse>>;

public class GetCostCentersHandler : IRequestHandler<GetCostCentersQuery, IEnumerable<CostCenterResponse>>
{
    private readonly AdminDbContext _db;

    public GetCostCentersHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<CostCenterResponse>> Handle(GetCostCentersQuery request, CancellationToken ct)
    {
        var query = _db.CostCenters
            .AsNoTracking()
            .Include(c => c.LegalEntity)
            .AsQueryable();

        if (request.LegalEntityId.HasValue)
            query = query.Where(c => c.LegalEntityId == request.LegalEntityId.Value);

        return await query
            .OrderBy(c => c.Code)
            .Select(c => new CostCenterResponse(
                c.Id, c.LegalEntityId, c.LegalEntity.Name, c.Code, c.Name, c.IsActive, c.CreatedAt, c.UpdatedAt))
            .ToListAsync(ct);
    }
}

// 2. Get Cost Center By Id
public record GetCostCenterByIdQuery(Guid Id) : IRequest<CostCenterResponse?>;

public class GetCostCenterByIdHandler : IRequestHandler<GetCostCenterByIdQuery, CostCenterResponse?>
{
    private readonly AdminDbContext _db;

    public GetCostCenterByIdHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<CostCenterResponse?> Handle(GetCostCenterByIdQuery request, CancellationToken ct)
    {
        var c = await _db.CostCenters
            .AsNoTracking()
            .Include(c => c.LegalEntity)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (c == null) return null;

        return new CostCenterResponse(
            c.Id, c.LegalEntityId, c.LegalEntity.Name, c.Code, c.Name, c.IsActive, c.CreatedAt, c.UpdatedAt);
    }
}
