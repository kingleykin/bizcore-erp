using Admin.API.Application.DTOs;
using Admin.API.Application.Services;
using Admin.API.Domain.Entities;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

public record LoginCommand(LoginRequest Request, string? IpAddress) : IRequest<LoginResponse>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly AdminDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        AdminDbContext db,
        ITokenService tokenService,
        IAuditPublisher audit,
        ILogger<LoginCommandHandler> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _audit = audit;
        _logger = logger;
    }

    public async Task<LoginResponse> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var request = command.Request;
        var user = await _db.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Username == request.Username.ToLowerInvariant(), cancellationToken);

        if (user == null)
        {
            _logger.LogWarning("AuthLoginFailed: UserNotFound {@AuthEvent}", new { Username = request.Username });
            throw new UnauthorizedException("Invalid username or password.");
        }

        if (!user.IsActive)
            throw new UnauthorizedException("Account is deactivated. Please contact an administrator.");

        if (user.IsLockedOut())
            throw new UnauthorizedException($"Account is locked until {user.LockoutEnd:O}. Please try again later.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.RecordFailedLogin();
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("AuthLoginFailed: InvalidPassword {@AuthEvent}", new { Username = user.Username });

            await _audit.PublishAsync(
                AuditActions.Identity.AuthLoginFailed,
                entityType: nameof(User), entityId: user.Id.ToString(),
                after: new { user.Username, user.FailedLoginAttempts },
                category: AuditCategory.Security,
                severity: AuditSeverity.Warning,
                outcome: AuditOutcome.Failure,
                classification: DataClassification.Credential,
                actorUsername: request.Username,
                ct: cancellationToken);

            throw new UnauthorizedException("Invalid username or password.");
        }

        user.ResetFailedLogins();

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var permissions = user.UserRoles
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .ToArray();

        var (accessToken, expiry) = _tokenService.GenerateJwt(user, roles, permissions);
        var refreshToken = await _tokenService.CreateRefreshTokenAsync(user.Id, command.IpAddress);

        await _db.SaveChangesAsync(cancellationToken);

        await _audit.PublishAsync(
            AuditActions.Identity.AuthLoginSucceeded,
            entityType: nameof(User), entityId: user.Id.ToString(),
            after: new { user.Username, Roles = roles },
            category: AuditCategory.Security,
            classification: DataClassification.Credential,
            ct: cancellationToken);

        _logger.LogInformation("AuthLoginSucceeded {@AuthEvent}", new { Username = user.Username, UserId = user.Id });

        return new LoginResponse(accessToken, refreshToken.Token, expiry, user.Id, user.Username, user.AvatarUrl, roles, permissions);
    }
}
