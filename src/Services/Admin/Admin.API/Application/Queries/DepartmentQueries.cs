using Admin.API.Application.DTOs;
using Admin.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Queries;

// 1. Get All Departments
public record GetDepartmentsQuery(Guid? BranchId = null) : IRequest<IEnumerable<DepartmentResponse>>;

public class GetDepartmentsHandler : IRequestHandler<GetDepartmentsQuery, IEnumerable<DepartmentResponse>>
{
    private readonly AdminDbContext _db;

    public GetDepartmentsHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<DepartmentResponse>> Handle(GetDepartmentsQuery request, CancellationToken ct)
    {
        var query = _db.Departments
            .AsNoTracking()
            .Include(d => d.Branch)
            .Include(d => d.Parent)
            .AsQueryable();

        if (request.BranchId.HasValue)
            query = query.Where(d => d.BranchId == request.BranchId.Value);

        var departments = await query.OrderBy(d => d.Code).ToListAsync(ct);
        
        return departments.Select(d => new DepartmentResponse(
            d.Id, d.BranchId, d.Branch.Name, d.ParentId, d.Parent?.Name, d.Code, d.Name, d.CreatedAt, d.UpdatedAt, new List<DepartmentResponse>()));
    }
}

// 2. Get Department By Id
public record GetDepartmentByIdQuery(Guid Id) : IRequest<DepartmentResponse?>;

public class GetDepartmentByIdHandler : IRequestHandler<GetDepartmentByIdQuery, DepartmentResponse?>
{
    private readonly AdminDbContext _db;

    public GetDepartmentByIdHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<DepartmentResponse?> Handle(GetDepartmentByIdQuery request, CancellationToken ct)
    {
        var d = await _db.Departments
            .AsNoTracking()
            .Include(d => d.Branch)
            .Include(d => d.Parent)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);

        if (d == null) return null;

        return new DepartmentResponse(
            d.Id, d.BranchId, d.Branch.Name, d.ParentId, d.Parent?.Name, d.Code, d.Name, d.CreatedAt, d.UpdatedAt, new List<DepartmentResponse>());
    }
}

// 3. Get Department Tree
public record GetDepartmentTreeQuery(Guid? BranchId = null) : IRequest<IEnumerable<DepartmentResponse>>;

public class GetDepartmentTreeHandler : IRequestHandler<GetDepartmentTreeQuery, IEnumerable<DepartmentResponse>>
{
    private readonly AdminDbContext _db;

    public GetDepartmentTreeHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<DepartmentResponse>> Handle(GetDepartmentTreeQuery request, CancellationToken ct)
    {
        var query = _db.Departments
            .AsNoTracking()
            .Include(d => d.Branch)
            .AsQueryable();

        if (request.BranchId.HasValue)
            query = query.Where(d => d.BranchId == request.BranchId.Value);

        var all = await query.ToListAsync(ct);
        
        var flat = all.Select(d => new DepartmentResponse(
            d.Id, d.BranchId, d.Branch.Name, d.ParentId, null, d.Code, d.Name, d.CreatedAt, d.UpdatedAt, new List<DepartmentResponse>()))
            .ToList();

        var lookup = flat.ToDictionary(x => x.Id);
        var roots = new List<DepartmentResponse>();

        foreach (var item in flat)
        {
            if (item.ParentId.HasValue && lookup.TryGetValue(item.ParentId.Value, out var parent))
            {
                parent.Children.Add(item);
            }
            else
            {
                roots.Add(item);
            }
        }

        return roots;
    }
}
