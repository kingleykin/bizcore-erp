using Admin.API.Infrastructure.Data;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Abstractions;
using Bizcore.BuildingBlocks.Exceptions;
using MediatR;

namespace Admin.API.Application.Commands;

public record UpdatePreferredLanguageCommand(Guid UserId, string LanguageCode) : IRequest, ITransactionalCommand;

public class UpdatePreferredLanguageCommandHandler : IRequestHandler<UpdatePreferredLanguageCommand>
{
    private readonly AdminDbContext _db;
    private readonly ILogger<UpdatePreferredLanguageCommandHandler> _logger;

    public UpdatePreferredLanguageCommandHandler(AdminDbContext db, ILogger<UpdatePreferredLanguageCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task Handle(UpdatePreferredLanguageCommand command, CancellationToken cancellationToken)
    {
        var user = await _db.Users.FindAsync(new object[] { command.UserId }, cancellationToken)
            ?? throw new NotFoundException(ErrorCodes.User.NotFound, "User not found", new { userId = command.UserId });

        user.SetPreferredLanguage(command.LanguageCode);

        _logger.LogInformation("Updated preferred language for user '{Id}' to '{Language}'.", command.UserId, command.LanguageCode);
    }
}
