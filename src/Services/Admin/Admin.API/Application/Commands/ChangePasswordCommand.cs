using Admin.API.Application.DTOs;
using Admin.API.Domain.Entities;
using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Admin.API.Application.Commands;

public record ChangePasswordCommand(Guid UserId, ChangePasswordRequest Request) : IRequest, ITransactionalCommand;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly AdminDbContext _db;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        AdminDbContext db,
        IAuditPublisher audit,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _db = db;
        _audit = audit;
        _logger = logger;
    }

    public async Task Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync(new object[] { command.UserId }, cancellationToken)
            ?? throw new NotFoundException("User", command.UserId);

        if (!BCrypt.Net.BCrypt.Verify(command.Request.CurrentPassword, user.PasswordHash))
            throw new UnauthorizedException("Current password is incorrect.");

        user.UpdatePassword(BCrypt.Net.BCrypt.HashPassword(command.Request.NewPassword));

        // Revoke all existing refresh tokens for security
        var tokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == command.UserId && !rt.IsRevoked)
            .ToListAsync(cancellationToken);
        tokens.ForEach(t => t.Revoke());

        await _audit.PublishAsync(
            AuditActions.Identity.AuthPasswordChanged,
            entityType: nameof(User), entityId: command.UserId.ToString(),
            after: new { Event = "PasswordChanged", UserId = command.UserId },
            category: AuditCategory.Security,
            severity: AuditSeverity.Critical,
            classification: DataClassification.Credential,
            ct: cancellationToken);

        _logger.LogInformation("Password changed for user '{UserId}'.", command.UserId);
    }
}
