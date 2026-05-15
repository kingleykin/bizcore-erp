using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Storage;
using MediatR;

namespace File.API.Application.Commands;

public record DeleteFileCommand(string FileName, bool IsPublic) : IRequest;

public class DeleteFileHandler : IRequestHandler<DeleteFileCommand>
{
    private readonly IStorageService _storageService;
    private readonly IAuditPublisher _audit;

    public DeleteFileHandler(IStorageService storageService, IAuditPublisher audit)
    {
        _storageService = storageService;
        _audit = audit;
    }

    public async Task Handle(DeleteFileCommand request, CancellationToken ct)
    {
        await _storageService.DeleteAsync(request.FileName, request.IsPublic, ct);

        await _audit.PublishAsync(
            AuditActions.File.Deleted,
            entityType: "File",
            entityId: request.FileName,
            category: AuditCategory.Business,
            severity: AuditSeverity.Warning);
    }
}
