using Bizcore.BuildingBlocks.Contracts;
using MassTransit;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Bizcore.BuildingBlocks.Authorization.Consumers
{
    /// <summary>
    /// Consumer lắng nghe sự kiện thay đổi quyền của Role từ Identity Service.
    /// Khi nhận được event, consumer sẽ thực hiện xóa cache permissions liên quan
    /// để đảm bảo tính real-time cho các microservice.
    /// </summary>
    public class RolePermissionsChangedConsumer : IConsumer<IRolePermissionsChangedEvent>
    {
        private readonly IPermissionCache _cache;
        private readonly ILogger<RolePermissionsChangedConsumer> _logger;

        public RolePermissionsChangedConsumer(IPermissionCache cache, ILogger<RolePermissionsChangedConsumer> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task Consume(ConsumeContext<IRolePermissionsChangedEvent> context)
        {
            _logger.LogInformation("RolePermissionsChangedEvent received for Role: {RoleName} ({RoleId}). Invalidating cache...", 
                context.Message.RoleName, context.Message.RoleId);

            // Xóa cache trong Redis (và local cache nếu có triển khai trong IPermissionCache)
            await _cache.InvalidateRoleAsync(context.Message.RoleId);
            
            _logger.LogDebug("Successfully invalidated permission cache for RoleId: {RoleId}", context.Message.RoleId);
        }
    }
}
