using Bizcore.BuildingBlocks.Exceptions;
using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities;
using Admin.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Bizcore.BuildingBlocks.Authorization;
using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using System.Diagnostics;

namespace Admin.API.Application.Services
{
    public class RoleService : IRoleService
    {
        private readonly AdminDbContext _db;
        private readonly ILogger<RoleService> _logger;
        private readonly IPermissionCache _cache;
        private readonly IAuditPublisher _audit;
        private readonly IPublishEndpoint _publishEndpoint;

        public RoleService(
            AdminDbContext db, 
            ILogger<RoleService> logger,
            IPermissionCache cache,
            IAuditPublisher audit,
            IPublishEndpoint publishEndpoint)
        {
            _db = db;
            _logger = logger;
            _cache = cache;
            _audit = audit;
            _publishEndpoint = publishEndpoint;
        }

        public async Task<IEnumerable<RoleDto>> GetAllAsync()
        {
            var roles = await _db.Roles
                .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .AsNoTracking()
                .ToListAsync();

            return roles.Select(MapToDto);
        }

        public async Task<RoleDto> GetByIdAsync(Guid id)
        {
            var role = await _db.Roles
                .Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission)
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id)
                ?? throw new NotFoundException("Role", id);

            return MapToDto(role);
        }

        public async Task<RoleDto> CreateAsync(CreateRoleRequest request)
        {
            if (await _db.Roles.AnyAsync(r => r.Name == request.Name.Trim()))
                throw new DomainException($"Role '{request.Name}' already exists.");

            var role = Role.Create(request.Name, request.Description);
            _db.Roles.Add(role);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created role '{Name}' (Id: {Id}).", role.Name, role.Id);
            return await GetByIdAsync(role.Id);
        }

        public async Task<RoleDto> UpdateAsync(Guid id, UpdateRoleRequest request)
        {
            var role = await _db.Roles.FindAsync(id)
                ?? throw new NotFoundException("Role", id);

            if (role.IsSystem)
                throw new DomainException("System roles cannot be modified.");

            if (await _db.Roles.AnyAsync(r => r.Name == request.Name.Trim() && r.Id != id))
                throw new DomainException($"Role name '{request.Name}' is already taken.");

            role.Update(request.Name, request.Description);
            await _db.SaveChangesAsync();

            return await GetByIdAsync(id);
        }

        public async Task DeleteAsync(Guid id)
        {
            var role = await _db.Roles.FindAsync(id)
                ?? throw new NotFoundException("Role", id);

            if (role.IsSystem)
                throw new DomainException("System roles cannot be deleted.");

            _db.Roles.Remove(role);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Deleted role '{Name}' (Id: {Id}).", role.Name, id);
        }

        public async Task AssignPermissionsAsync(Guid roleId, AssignPermissionsRequest request)
        {
            var role = await _db.Roles.FindAsync(roleId)
                ?? throw new NotFoundException("Role", roleId);

            var permissionIds = request.PermissionIds.Distinct().ToList();
            var existingPerms = await _db.Permissions
                .Where(p => permissionIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync();

            if (existingPerms.Count != permissionIds.Count)
                throw new DomainException("One or more permission IDs are invalid.");

            // Full replace strategy
            var currentPerms = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync();
            _db.RolePermissions.RemoveRange(currentPerms);

            var newPerms = permissionIds.Select(pid => new RolePermission
            {
                RoleId = roleId,
                PermissionId = pid
            });
            _db.RolePermissions.AddRange(newPerms);
            await _db.SaveChangesAsync();

            // Invalidate cache for all users in this role
            await _cache.InvalidateRoleAsync(roleId);

            // Publish event
            await _publishEndpoint.Publish<IRolePermissionsChangedEvent>(new
            {
                RoleId = role.Id,
                RoleName = role.Name,
                ChangedAt = DateTime.UtcNow
            });

            // Audit
            await _audit.PublishAsync(
                AuditActions.Identity.RolePermissionsAssigned,
                entityType: nameof(Role), entityId: role.Id.ToString(),
                after: new { role.Name, PermissionCount = permissionIds.Count },
                category: AuditCategory.Security);

            _logger.LogInformation("Assigned {Count} permission(s) to role '{Name}'.", permissionIds.Count, role.Name);
        }

        public async Task<IEnumerable<PermissionDto>> GetAllPermissionsAsync()
        {
            var perms = await _db.Permissions.AsNoTracking().ToListAsync();
            return perms.Select(p => new PermissionDto(p.Id, p.Code, p.Name, p.Resource, p.Scope, p.Description));
        }

        private static RoleDto MapToDto(Role r) => new(
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
                rp.Permission.Description))
        );

    }
}
