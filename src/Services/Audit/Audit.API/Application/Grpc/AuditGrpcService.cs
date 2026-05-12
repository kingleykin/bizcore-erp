using Audit.API.Application.Services;
using Audit.API.Application.DTOs;
using Audit.API.Domain.Entities;
using Audit.API.Infrastructure.Data;
using Audit.API.Infrastructure.Services;
using Bizcore.BuildingBlocks.Grpc.Protos;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;

namespace Audit.API.Application.Grpc
{
    public class AuditGrpcService : AuditGrpc.AuditGrpcBase
    {
        private readonly IAuditQueryService _queryService;
        private readonly AuditDbContext _db;
        private readonly HashChainService _hashChain;
        private readonly ILogger<AuditGrpcService> _logger;

        public AuditGrpcService(
            IAuditQueryService queryService,
            AuditDbContext db,
            HashChainService hashChain,
            ILogger<AuditGrpcService> logger)
        {
            _queryService = queryService;
            _db = db;
            _hashChain = hashChain;
            _logger = logger;
        }

        public override async Task<GetAuditLogsResponse> GetAuditLogs(GetAuditLogsRequest request, ServerCallContext context)
        {
            var result = await _queryService.QueryAsync(new AuditQueryParams
            {
                EntityId = request.ResourceId,
                PageSize = request.Limit > 0 ? request.Limit : 10
            }, context.CancellationToken);

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
}
