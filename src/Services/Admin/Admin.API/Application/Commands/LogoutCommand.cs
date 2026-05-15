using Admin.API.Infrastructure.Data;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

public record LogoutCommand(string RefreshToken) : IRequest;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand>
{
    private readonly AdminDbContext _db;
    private readonly ILogger<LogoutCommandHandler> _logger;

    public LogoutCommandHandler(AdminDbContext db, ILogger<LogoutCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var storedToken = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);
        if (storedToken == null || !storedToken.IsActive())
            return;

        storedToken.Revoke();
        await _db.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Refresh token revoked for user '{UserId}'.", storedToken.UserId);
    }
}
