using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;
using MediatR;

namespace Admin.API.Application.Commands;

public record UpdateAvatarCommand(Guid UserId, string? AvatarUrl) : IRequest, ITransactionalCommand;

public class UpdateAvatarCommandHandler : IRequestHandler<UpdateAvatarCommand>
{
    private readonly AdminDbContext _db;
    private readonly ILogger<UpdateAvatarCommandHandler> _logger;

    public UpdateAvatarCommandHandler(AdminDbContext db, ILogger<UpdateAvatarCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(UpdateAvatarCommand command, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync(new object[] { command.UserId }, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.User.NotFound, "User not found", new { userId = command.UserId });

        user.UpdateAvatar(command.AvatarUrl);

        _logger.LogInformation("Updated avatar for user '{Id}'.", command.UserId);
    }
}
