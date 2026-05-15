using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

// 1. Create Role
public record CreateRoleCommand(CreateRoleRequest Request) : IRequest<RoleDto>, ITransactionalCommand;

public class CreateRoleHandler : IRequestHandler<CreateRoleCommand, RoleDto>
{
    private readonly AdminDbContext _db;

    public CreateRoleHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<RoleDto> Handle(CreateRoleCommand command, CancellationToken ct)
    {
        var role = Role.Create(command.Request.Name, command.Request.Description);

        _db.Roles.Add(role);

        return new RoleDto(
            role.Id,
            role.Name,
            role.Description,
            role.IsSystem,
            new List<PermissionDto>()
        );
    }
}

// 2. Update Role
public record UpdateRoleCommand(Guid Id, UpdateRoleRequest Request) : IRequest<RoleDto>, ITransactionalCommand;

public class UpdateRoleHandler : IRequestHandler<UpdateRoleCommand, RoleDto>
{
    private readonly AdminDbContext _db;

    public UpdateRoleHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<RoleDto> Handle(UpdateRoleCommand command, CancellationToken ct)
    {
        var role = await _db.Roles
            .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(r => r.Id == command.Id, ct);

        if (role == null) throw new KeyNotFoundException($"Role {command.Id} not found");

        role.Update(command.Request.Name, command.Request.Description);

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

// 3. Delete Role
public record DeleteRoleCommand(Guid Id) : IRequest, ITransactionalCommand;

public class DeleteRoleHandler : IRequestHandler<DeleteRoleCommand>
{
    private readonly AdminDbContext _db;

    public DeleteRoleHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task Handle(DeleteRoleCommand command, CancellationToken ct)
    {
        var role = await _db.Roles.FindAsync(new object[] { command.Id }, ct);
        if (role != null)
        {
            if (role.IsSystem) throw new InvalidOperationException("Cannot delete system role");
            _db.Roles.Remove(role);
        }
    }
}

// 4. Assign Permissions
public record AssignRolePermissionsCommand(Guid RoleId, AssignPermissionsRequest Request) : IRequest, ITransactionalCommand;

public class AssignRolePermissionsHandler : IRequestHandler<AssignRolePermissionsCommand>
{
    private readonly AdminDbContext _db;

    public AssignRolePermissionsHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task Handle(AssignRolePermissionsCommand command, CancellationToken ct)
    {
        var role = await _db.Roles.Include(r => r.RolePermissions).FirstOrDefaultAsync(r => r.Id == command.RoleId, ct);
        if (role == null) throw new KeyNotFoundException($"Role {command.RoleId} not found");

        if (role.IsSystem) throw new InvalidOperationException("Cannot manage permissions for system roles");

        // Remove existing
        _db.Set<RolePermission>().RemoveRange(role.RolePermissions);

        // Add new
        foreach (var permId in command.Request.PermissionIds)
        {
            role.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permId });
        }
    }
}
