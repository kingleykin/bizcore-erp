using Bizcore.BuildingBlocks.Grpc;
using Bizcore.BuildingBlocks.Grpc.Protos;
using Bizcore.BuildingBlocks.Contracts;
using Grpc.Core;
using MassTransit;

namespace Orchestration.API.Application.Services
{
    public interface IAuditClientService
    {
        Task<List<AuditLogModel>> GetResourceLogsAsync(string resourceId, int limit = 10);
        Task LogAsync(string action, string entityType, string entityId, string details);
    }

    public class AuditClientService : IAuditClientService
    {
        private readonly AuditGrpc.AuditGrpcClient _client;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<AuditClientService> _logger;

        public AuditClientService(
            AuditGrpc.AuditGrpcClient client, 
            IPublishEndpoint publishEndpoint,
            ILogger<AuditClientService> logger)
        {
            _client = client;
            _publishEndpoint = publishEndpoint;
            _logger = logger;
        }

        /// <summary>
        /// Query gRPC (Synchronous)
        /// </summary>
        public async Task<List<AuditLogModel>> GetResourceLogsAsync(string resourceId, int limit = 10)
        {
            try
            {
                var response = await _client.GetAuditLogsAsync(new GetAuditLogsRequest
                {
                    ResourceId = resourceId,
                    Limit = limit
                });

                return response.Logs.ToList();
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Failed to query Audit logs via gRPC");
                throw GrpcErrorMapper.MapToDomainException(ex, "AuditService");
            }
        }

        /// <summary>
        /// Command Messaging (Asynchronous)
        /// </summary>
        public async Task LogAsync(string action, string entityType, string entityId, string details)
        {
            // Tuân thủ kiến trúc: Command/State change phải dùng Messaging
            await _publishEndpoint.Publish<AuditEvent>(new
            {
                ServiceName = "Orchestration.API",
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                AfterJson = details,
                AuditLevel = "Information",
                OccurredAt = DateTime.UtcNow
            });
        }
    }
}
