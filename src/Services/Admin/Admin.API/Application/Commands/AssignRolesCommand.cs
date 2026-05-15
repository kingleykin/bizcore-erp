using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Authorization;
using Bizcore.BuildingBlocks.Contracts;
using Bizcore.BuildingBlocks.Exceptions;
using MassTransit;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

public record AssignRolesCommand(Guid UserId, AssignRolesRequest Request) : IRequest, ITransactionalCommand;

public class AssignRolesCommandHandler : IRequestHandler<AssignRolesCommand>
{
    private readonly AdminDbContext _db;
    private readonly IPermissionCache _cache;
    private readonly IAuditPublisher _audit;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<AssignRolesCommandHandler> _logger;

    public AssignRolesCommandHandler(
        AdminDbContext db,
        IPermissionCache cache,
        IAuditPublisher audit,
        IPublishEndpoint publishEndpoint,
        ILogger<AssignRolesCommandHandler> logger)
    {
        _db = db;
        _cache = cache;
        _audit = audit;
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task Handle(AssignRolesCommand command, CancellationToken cancellationToken)
    {
        var userId = command.UserId;
        var request = command.Request;

        var user = await _db.Users.FindAsync(new object[] { userId }, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        var roleIds = request.RoleIds.Distinct().ToList();
        var existingRoles = await _db.Roles.Where(r => roleIds.Contains(r.Id)).Select(r => r.Id).ToListAsync(cancellationToken);

        if (existingRoles.Count != roleIds.Count)
            throw new DomainException("One or more role IDs are invalid.");

        var currentRoles = await _db.UserRoles.Where(ur => ur.UserId == userId).ToListAsync(cancellationToken);
        _db.UserRoles.RemoveRange(currentRoles);

        var newUserRoles = roleIds.Select(rid => new UserRole
        {
            UserId = userId,
            RoleId = rid,
            AssignedAt = DateTime.UtcNow
        });
        _db.UserRoles.AddRange(newUserRoles);

        await _cache.InvalidateAsync(userId);

        await _publishEndpoint.Publish<IUserPermissionsChangedEvent>(new
        {
            UserId = userId,
            ChangedAt = DateTime.UtcNow
        }, cancellationToken);

        await _audit.PublishAsync(
            AuditActions.Identity.UserRolesAssigned,
            entityType: nameof(User), entityId: userId.ToString(),
            after: new { userId, RoleCount = roleIds.Count },
            category: AuditCategory.Security,
            ct: cancellationToken);

        _logger.LogInformation("Assigned {Count} role(s) to user '{Id}'.", roleIds.Count, userId);
    }
}
