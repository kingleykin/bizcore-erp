using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

public record UpdateUserCommand(Guid Id, UpdateUserRequest Request) : IRequest<UserDto>, ITransactionalCommand;

public class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserDto>
{
    private readonly AdminDbContext _db;
    private readonly ILogger<UpdateUserCommandHandler> _logger;

    public UpdateUserCommandHandler(AdminDbContext db, ILogger<UpdateUserCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<UserDto> Handle(UpdateUserCommand command, CancellationToken cancellationToken)
    {
        var id = command.Id;
        var request = command.Request;

        var user = await _db.Users
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.User.NotFound, "User not found", new { id });

        if (await _db.Users.AnyAsync(u => u.Email == request.Email.ToLowerInvariant() && u.Id != id, cancellationToken))
            throw new DomainException(ErrorCodes.User.EmailTaken, $"Email '{request.Email}' is already registered by another user.");

        user.UpdateProfile(request.Email);

        _logger.LogInformation("Updated user '{Id}'.", id);
        return MapToDto(user);
    }

    private static UserDto MapToDto(User u) => new(
        u.Id, u.Username, u.Email, u.AvatarUrl, u.IsActive, u.FailedLoginAttempts, u.LockoutEnd, u.CreatedAt, u.UserRoles.Select(ur => ur.Role.Name)
    );
}
