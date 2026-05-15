using Admin.API.Application.DTOs;
using Admin.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Queries;

// 1. Get All Users
public record GetUsersQuery : IRequest<IEnumerable<UserDto>>;

public class GetUsersHandler : IRequestHandler<GetUsersQuery, IEnumerable<UserDto>>
{
    private readonly AdminDbContext _db;

    public GetUsersHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<IEnumerable<UserDto>> Handle(GetUsersQuery request, CancellationToken ct)
    {
        return await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Select(u => new UserDto(
                u.Id,
                u.Username ?? string.Empty,
                u.Email ?? string.Empty,
                u.AvatarUrl,
                u.IsActive,
                u.FailedLoginAttempts,
                u.LockoutEnd,
                u.CreatedAt,
                u.UserRoles.Select(ur => ur.Role.Name).ToList()
            ))
            .ToListAsync(ct);
    }
}

// 2. Get User By Id
public record GetUserByIdQuery(Guid Id) : IRequest<UserDto?>;

public class GetUserByIdHandler : IRequestHandler<GetUserByIdQuery, UserDto?>
{
    private readonly AdminDbContext _db;

    public GetUserByIdHandler(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<UserDto?> Handle(GetUserByIdQuery request, CancellationToken ct)
    {
        var user = await _db.Users
            .AsNoTracking()
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == request.Id, ct);

        if (user == null) return null;

        return new UserDto(
            user.Id,
            user.Username ?? string.Empty,
            user.Email ?? string.Empty,
            user.AvatarUrl,
            user.IsActive,
            user.FailedLoginAttempts,
            user.LockoutEnd,
            user.CreatedAt,
            user.UserRoles.Select(ur => ur.Role.Name).ToList()
        );
    }
}
