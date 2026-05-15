using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

public record CreateUserCommand(CreateUserRequest Request) : IRequest<UserDto>, ITransactionalCommand;

public class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, UserDto>
{
    private readonly AdminDbContext _db;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(AdminDbContext db, ILogger<CreateUserCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<UserDto> Handle(CreateUserCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;

        if (await _db.Users.AnyAsync(u => u.Username == request.Username.ToLowerInvariant(), cancellationToken))
            throw new DomainException(ErrorCodes.User.UsernameTaken, $"Username '{request.Username}' is already taken.");

        if (await _db.Users.AnyAsync(u => u.Email == request.Email.ToLowerInvariant(), cancellationToken))
            throw new DomainException(ErrorCodes.User.EmailTaken, $"Email '{request.Email}' is already registered.");

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = User.Create(request.Username, request.Email, passwordHash);

        _db.Users.Add(user);
        // No SaveChangesAsync here — TransactionBehavior commits everything atomically at the end.

        _logger.LogInformation("Created user '{Username}' (Id: {Id}).", user.Username, user.Id);

        if (request.RoleNames != null && request.RoleNames.Any())
        {
            var roleIds = await _db.Roles
                .Where(r => request.RoleNames.Contains(r.Name))
                .Select(r => r.Id)
                .ToListAsync(cancellationToken);

            if (roleIds.Any())
            {
                // Assign roles directly in this handler (same transaction) instead of dispatching
                // a nested mediator command which would start a new transaction pipeline.
                var currentRoles = await _db.UserRoles.Where(ur => ur.UserId == user.Id).ToListAsync(cancellationToken);
                _db.UserRoles.RemoveRange(currentRoles);

                var newUserRoles = roleIds.Select(rid => new UserRole
                {
                    UserId = user.Id,
                    RoleId = rid,
                    AssignedAt = DateTime.UtcNow
                });
                _db.UserRoles.AddRange(newUserRoles);
            }
        }

        var roleNames = request.RoleNames ?? Array.Empty<string>();
        return MapToDto(user, roleNames);
    }

    private static UserDto MapToDto(User u, IEnumerable<string> roles) => new(
        u.Id, u.Username, u.Email, u.AvatarUrl, u.IsActive, u.FailedLoginAttempts, u.LockoutEnd, u.CreatedAt, roles
    );
}
