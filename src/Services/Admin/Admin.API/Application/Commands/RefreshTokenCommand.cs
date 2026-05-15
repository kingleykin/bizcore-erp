using Admin.API.Application.DTOs;
using Admin.API.Application.Services;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

public record RefreshTokenCommand(string RefreshToken, string? IpAddress) : IRequest<LoginResponse>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, LoginResponse>
{
    private readonly AdminDbContext _db;
    private readonly ITokenService _tokenService;

    public RefreshTokenCommandHandler(AdminDbContext db, ITokenService tokenService)
    {
        _db = db;
        _tokenService = tokenService;
    }

    public async Task<LoginResponse> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var storedToken = await _db.RefreshTokens
            .Include(rt => rt.User)
                .ThenInclude(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

        if (storedToken == null || !storedToken.IsActive())
            throw new UnauthorizedException("Invalid or expired refresh token.");

        var user = storedToken.User;
        if (!user.IsActive)
            throw new UnauthorizedException("Account is deactivated.");

        storedToken.Revoke();
        var newRefreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id, request.IpAddress);

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToArray();

        var (accessToken, expiry) = _tokenService.GenerateJwt(user, roles, permissions);
        await _db.SaveChangesAsync(cancellationToken);

        return new LoginResponse(accessToken, newRefreshToken.Token, expiry, user.Id, user.Username, user.AvatarUrl, roles, permissions);
    }
}
