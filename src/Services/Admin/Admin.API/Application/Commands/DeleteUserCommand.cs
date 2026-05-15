using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;
using MediatR;

namespace Admin.API.Application.Commands;

public record DeleteUserCommand(Guid Id) : IRequest, ITransactionalCommand;

public class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand>
{
    private readonly AdminDbContext _db;
    private readonly ILogger<DeleteUserCommandHandler> _logger;

    public DeleteUserCommandHandler(AdminDbContext db, ILogger<DeleteUserCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(DeleteUserCommand command, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync(new object[] { command.Id }, cancellationToken)
            ?? throw new NotFoundException("User", command.Id);

        user.Deactivate();

        _logger.LogInformation("Deactivated user '{Username}' (Id: {Id}).", user.Username, command.Id);
    }
}
