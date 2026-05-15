using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Storage;
using File.API.Application.DTOs;
using MediatR;

namespace File.API.Application.Commands;

public record UploadFileCommand(Stream Stream, string FileName, string ContentType, bool IsPublic) : IRequest<FileUploadResponse>;

public class UploadFileHandler : IRequestHandler<UploadFileCommand, FileUploadResponse>
{
    private readonly IStorageService _storageService;
    private readonly IAuditPublisher _audit;

    public UploadFileHandler(IStorageService storageService, IAuditPublisher audit)
    {
        _storageService = storageService;
        _audit = audit;
    }

    public async Task<FileUploadResponse> Handle(UploadFileCommand request, CancellationToken ct)
    {
        var finalFileName = $"{Guid.NewGuid()}{Path.GetExtension(request.FileName)}";
        
        var result = await _storageService.UploadAsync(request.Stream, finalFileName, request.ContentType, request.IsPublic, ct);

        await _audit.PublishAsync(
            AuditActions.File.Uploaded,
            entityType: "File",
            entityId: finalFileName,
            after: new { finalFileName, request.ContentType, request.IsPublic },
            category: AuditCategory.Business);

        string? url = null;
        if (request.IsPublic)
        {
            url = await _storageService.GetFileUrlAsync(finalFileName, true, cancellationToken: ct);
        }

        return new FileUploadResponse(finalFileName, url);
    }
}
