using Admin.API.Application.DTOs;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Admin.API.Application.Queries;

public record GetMyNavigationQuery(ClaimsPrincipal User) : IRequest<NavigationMenuDto[]?>;

public class GetMyNavigationHandler : IRequestHandler<GetMyNavigationQuery, NavigationMenuDto[]?>
{
    private readonly AdminDbContext _db;
    private readonly IPermissionCache? _cache;

    public GetMyNavigationHandler(AdminDbContext db, IPermissionCache? cache = null)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<NavigationMenuDto[]?> Handle(GetMyNavigationQuery request, CancellationToken ct)
    {
        var userId = GetUserId(request.User);
        if (userId == null) return null;

        string[] permissions;
        var cached = await (_cache?.GetAsync(userId.Value, ct) ?? Task.FromResult<string[]?>(null));
        if (cached != null)
        {
            permissions = cached;
        }
        else
        {
            var user = await _db.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);

            if (user == null) return null;

            permissions = user.UserRoles
                .SelectMany(ur => ur.Role.RolePermissions)
                .Select(rp => rp.Permission.Code)
                .Distinct().ToArray();

            if (_cache != null)
            {
                await _cache.SetAsync(userId.Value, permissions, ct);
                foreach (var role in user.UserRoles)
                {
                    await _cache.TrackUserInRoleAsync(userId.Value, role.RoleId);
                }
            }
        }

        var permissionSet = new HashSet<string>(permissions, StringComparer.OrdinalIgnoreCase);

        var menus = await _db.NavigationMenus
            .Where(m => m.IsActive && permissionSet.Contains(m.PermissionCode))
            .OrderBy(m => m.SortOrder)
            .AsNoTracking()
            .ToListAsync(ct);

        return menus.Select(m => new NavigationMenuDto(
            m.Id, m.ParentId, m.Name, m.Route, m.Icon, m.SortOrder
        )).ToArray();
    }

    private Guid? GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
