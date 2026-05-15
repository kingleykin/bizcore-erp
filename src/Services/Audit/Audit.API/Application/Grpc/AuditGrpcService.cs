using Audit.API.Application.Queries;
using Audit.API.Application.DTOs;
using Bizcore.BuildingBlocks.Grpc.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;

namespace Audit.API.Application.Grpc;

public class AuditGrpcService : AuditGrpc.AuditGrpcBase
{
    private readonly IMediator _mediator;

    public AuditGrpcService(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override async Task<GetAuditLogsResponse> GetAuditLogs(GetAuditLogsRequest request, ServerCallContext context)
    {
        var queryParams = new AuditQueryParams
        {
            EntityId = request.ResourceId,
            PageSize = request.Limit > 0 ? request.Limit : 10
        };

        var result = await _mediator.Send(new GetAuditEntriesQuery(queryParams), context.CancellationToken);

        var response = new GetAuditLogsResponse();
        response.Logs.AddRange(result.Items.Select(l => new AuditLogModel
        {
            Id = l.Id.ToString(),
            Action = l.Action,
            UserId = l.PerformedBy ?? "system",
            ResourceName = l.EntityName ?? "",
            ResourceId = l.EntityId ?? "",
            Details = l.AfterJson ?? "",
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(l.PerformedAt, DateTimeKind.Utc))
        }));

        return response;
    }
}
