using Admin.API.Application.DTOs;
using Admin.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Queries;

// 1. Get All Roles
public record GetRolesQuery : IRequest<IEnumerable<RoleDto>>;

public class GetRolesHandler : IRequestHandler<GetRolesQuery, IEnumerable<RoleDto>>
{
    private readonly AdminDbContext _db;

    public GetRolesHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<RoleDto>> Handle(GetRolesQuery request, CancellationToken ct)
    {
        return await _db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .Select(r => new RoleDto(
                r.Id,
                r.Name,
                r.Description,
                r.IsSystem,
                r.RolePermissions.Select(rp => new PermissionDto(
                    rp.Permission.Id,
                    rp.Permission.Code,
                    rp.Permission.Name,
                    rp.Permission.Resource,
                    rp.Permission.Scope,
                    rp.Permission.Description
                )).ToList()
            ))
            .ToListAsync(ct);
    }
}

// 2. Get Role By Id
public record GetRoleByIdQuery(Guid Id) : IRequest<RoleDto?>;

public class GetRoleByIdHandler : IRequestHandler<GetRoleByIdQuery, RoleDto?>
{
    private readonly AdminDbContext _db;

    public GetRoleByIdHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<RoleDto?> Handle(GetRoleByIdQuery request, CancellationToken ct)
    {
        var role = await _db.Roles
            .AsNoTracking()
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == request.Id, ct);

        if (role == null) return null;

        return new RoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystem,
            role.RolePermissions.Select(rp => new PermissionDto(
                rp.Permission.Id,
                rp.Permission.Code,
                rp.Permission.Name,
                rp.Permission.Resource,
                rp.Permission.Scope,
                rp.Permission.Description
            )).ToList()
        );
    }
}

// 3. Get All Permissions
public record GetPermissionsQuery : IRequest<IEnumerable<PermissionDto>>;

public class GetPermissionsHandler : IRequestHandler<GetPermissionsQuery, IEnumerable<PermissionDto>>
{
    private readonly AdminDbContext _db;

    public GetPermissionsHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<PermissionDto>> Handle(GetPermissionsQuery request, CancellationToken ct)
    {
        return await _db.Permissions
            .AsNoTracking()
            .Select(p => new PermissionDto(
                p.Id,
                p.Code,
                p.Name,
                p.Resource,
                p.Scope,
                p.Description
            ))
            .ToListAsync(ct);
    }
}
