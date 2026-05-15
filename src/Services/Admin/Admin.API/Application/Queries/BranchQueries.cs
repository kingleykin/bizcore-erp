using Admin.API.Application.DTOs;
using Admin.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Queries;

// 1. Get All Branches
public record GetBranchesQuery(Guid? LegalEntityId = null) : IRequest<IEnumerable<BranchResponse>>;

public class GetBranchesHandler : IRequestHandler<GetBranchesQuery, IEnumerable<BranchResponse>>
{
    private readonly AdminDbContext _db;

    public GetBranchesHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<BranchResponse>> Handle(GetBranchesQuery request, CancellationToken ct)
    {
        var query = _db.Branches
            .AsNoTracking()
            .Include(b => b.LegalEntity)
            .AsQueryable();

        if (request.LegalEntityId.HasValue)
            query = query.Where(b => b.LegalEntityId == request.LegalEntityId.Value);

        return await query
            .OrderBy(b => b.Code)
            .Select(b => new BranchResponse(
                b.Id, b.LegalEntityId, b.LegalEntity.Name, b.Code, b.Name, b.Address, b.IsActive, b.CreatedAt, b.UpdatedAt))
            .ToListAsync(ct);
    }
}

// 2. Get Branch By Id
public record GetBranchByIdQuery(Guid Id) : IRequest<BranchResponse?>;

public class GetBranchByIdHandler : IRequestHandler<GetBranchByIdQuery, BranchResponse?>
{
    private readonly AdminDbContext _db;

    public GetBranchByIdHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<BranchResponse?> Handle(GetBranchByIdQuery request, CancellationToken ct)
    {
        var b = await _db.Branches
            .AsNoTracking()
            .Include(b => b.LegalEntity)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (b == null) return null;

        return new BranchResponse(
            b.Id, b.LegalEntityId, b.LegalEntity.Name, b.Code, b.Name, b.Address, b.IsActive, b.CreatedAt, b.UpdatedAt);
    }
}
