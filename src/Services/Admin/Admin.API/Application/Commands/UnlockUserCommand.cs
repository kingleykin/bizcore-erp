using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;
using MediatR;

namespace Admin.API.Application.Commands;

public record UnlockUserCommand(Guid Id) : IRequest, ITransactionalCommand;

public class UnlockUserCommandHandler : IRequestHandler<UnlockUserCommand>
{
    private readonly AdminDbContext _db;
    private readonly ILogger<UnlockUserCommandHandler> _logger;

    public UnlockUserCommandHandler(AdminDbContext db, ILogger<UnlockUserCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(UnlockUserCommand command, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync(new object[] { command.Id }, cancellationToken)
            ?? throw new NotFoundException("User", command.Id);

        user.ResetFailedLogins();
        user.Activate();

        _logger.LogInformation("Unlocked user '{Username}' (Id: {Id}).", user.Username, command.Id);
    }
}
