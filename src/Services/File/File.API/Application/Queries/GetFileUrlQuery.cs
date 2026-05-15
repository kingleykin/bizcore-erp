using Bizcore.BuildingBlocks.Audit;
using Bizcore.BuildingBlocks.Storage;
using File.API.Application.DTOs;
using MediatR;

namespace File.API.Application.Queries;

public record GetFileUrlQuery(string FileName, bool IsPublic) : IRequest<FileUrlResponse>;

public class GetFileUrlHandler : IRequestHandler<GetFileUrlQuery, FileUrlResponse>
{
    private readonly IStorageService _storageService;
    private readonly IAuditPublisher _audit;

    public GetFileUrlHandler(IStorageService storageService, IAuditPublisher audit)
    {
        _storageService = storageService;
        _audit = audit;
    }

    public async Task<FileUrlResponse> Handle(GetFileUrlQuery request, CancellationToken ct)
    {
        var url = await _storageService.GetFileUrlAsync(request.FileName, request.IsPublic, 3600, ct);

        await _audit.PublishAsync(
            AuditActions.File.Viewed,
            entityType: "File",
            entityId: request.FileName,
            category: AuditCategory.Business);

        return new FileUrlResponse(url);
    }
}
