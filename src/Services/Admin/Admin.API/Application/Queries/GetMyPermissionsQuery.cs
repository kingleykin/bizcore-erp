using Admin.API.Application.DTOs;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Authorization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Admin.API.Application.Queries;

public record GetMyPermissionsQuery(ClaimsPrincipal User) : IRequest<UserPermissionsDto?>;

public class GetMyPermissionsHandler : IRequestHandler<GetMyPermissionsQuery, UserPermissionsDto?>
{
    private readonly AdminDbContext _db;
    private readonly IPermissionCache? _cache;
    private readonly ILogger<GetMyPermissionsHandler> _logger;

    public GetMyPermissionsHandler(AdminDbContext db, ILogger<GetMyPermissionsHandler> logger, IPermissionCache? cache = null)
    {
        _db = db;
        _logger = logger;
        _cache = cache;
    }

    public async Task<UserPermissionsDto?> Handle(GetMyPermissionsQuery request, CancellationToken ct)
    {
        var userId = GetUserId(request.User);
        if (userId == null) return null;

        // 1. Try Cache
        var cached = await (_cache?.GetAsync(userId.Value, ct) ?? Task.FromResult<string[]?>(null));
        if (cached != null)
        {
            var username = request.User.FindFirstValue(ClaimTypes.Name) ?? request.User.FindFirstValue("unique_name") ?? "unknown";
            var roles = request.User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).ToArray();
            return new UserPermissionsDto(userId.Value, username, roles, cached);
        }

        // 2. Fallback: DB
        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role).ThenInclude(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value, ct);

        if (user == null) return null;

        var userRoles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct().OrderBy(p => p).ToArray();

        // 3. Update Cache
        if (_cache != null)
        {
            await _cache.SetAsync(userId.Value, permissions, ct);
            foreach (var role in user.UserRoles)
            {
                await _cache.TrackUserInRoleAsync(userId.Value, role.RoleId);
            }
        }

        return new UserPermissionsDto(userId.Value, user.Username, userRoles, permissions);
    }

    private Guid? GetUserId(ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }
}
