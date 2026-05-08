using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace Bizcore.BuildingBlocks.Authorization
{
    /// <summary>
    /// Redis-backed permission cache.
    /// Key pattern: "user_permissions:{userId}"
    /// TTL: 5 phút (configurable).
    /// 
    /// Khi admin thay đổi role/permission → InvalidateAsync/InvalidateRoleAsync
    /// để xóa cache → request tiếp theo sẽ re-fetch từ DB.
    /// </summary>
    public class RedisPermissionCache : IPermissionCache
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisPermissionCache> _logger;
        private readonly TimeSpan _ttl;

        private const string UserPermissionsKeyPrefix = "user_permissions:";
        private const string UsersByRoleKeyPrefix      = "role_users:";

        public RedisPermissionCache(
            IConnectionMultiplexer redis,
            ILogger<RedisPermissionCache> logger,
            TimeSpan? ttl = null)
        {
            _redis  = redis;
            _logger = logger;
            _ttl    = ttl ?? TimeSpan.FromMinutes(5);
        }

        public async Task<string[]?> GetAsync(Guid userId, CancellationToken ct = default)
        {
            var db  = _redis.GetDatabase();
            var key = UserPermissionsKey(userId);

            try
            {
                var value = await db.StringGetAsync(key);
                if (value.IsNullOrEmpty) return null;

                return JsonSerializer.Deserialize<string[]>((string)value!);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache read failed for user {UserId}. Falling back to DB.", userId);
                return null; // Cache miss — caller sẽ load từ DB
            }
        }

        public async Task SetAsync(Guid userId, string[] permissions, CancellationToken ct = default)
        {
            var db  = _redis.GetDatabase();
            var key = UserPermissionsKey(userId);

            try
            {
                var json = JsonSerializer.Serialize(permissions);
                await db.StringSetAsync(key, json, _ttl);

                _logger.LogDebug("Cached {Count} permissions for user {UserId}.", permissions.Length, userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache write failed for user {UserId}.", userId);
            }
        }

        public async Task InvalidateAsync(Guid userId, CancellationToken ct = default)
        {
            var db  = _redis.GetDatabase();
            var key = UserPermissionsKey(userId);

            try
            {
                await db.KeyDeleteAsync(key);
                _logger.LogInformation("Permission cache invalidated for user {UserId}.", userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis cache invalidation failed for user {UserId}.", userId);
            }
        }

        public async Task InvalidateRoleAsync(Guid roleId, CancellationToken ct = default)
        {
            // Lấy danh sách userIds trong role từ Redis set
            var db       = _redis.GetDatabase();
            var roleKey  = UsersByRoleKey(roleId);

            try
            {
                var members = await db.SetMembersAsync(roleKey);
                if (members.Length == 0)
                {
                    _logger.LogDebug("No cached users found for role {RoleId}.", roleId);
                    return;
                }

                // Xóa cache của từng user
                var userKeys = members
                    .Select(m => (RedisKey)UserPermissionsKey(Guid.Parse((string)m!)))
                    .ToArray();

                await db.KeyDeleteAsync(userKeys);
                await db.KeyDeleteAsync(roleKey); // Xóa set tracking luôn

                _logger.LogInformation(
                    "Permission cache invalidated for {Count} users in role {RoleId}.",
                    members.Length, roleId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Redis role cache invalidation failed for role {RoleId}.", roleId);
            }
        }

        /// <summary>
        /// Đăng ký userId vào role tracking set để hỗ trợ InvalidateRoleAsync.
        /// Gọi khi load permissions lần đầu hoặc khi assign user vào role.
        /// </summary>
        public async Task TrackUserInRoleAsync(Guid userId, Guid roleId)
        {
            var db = _redis.GetDatabase();
            try
            {
                await db.SetAddAsync(UsersByRoleKey(roleId), userId.ToString(), CommandFlags.FireAndForget);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to track user {UserId} in role {RoleId}.", userId, roleId);
            }
        }

        private static string UserPermissionsKey(Guid userId) => $"{UserPermissionsKeyPrefix}{userId}";
        private static string UsersByRoleKey(Guid roleId)     => $"{UsersByRoleKeyPrefix}{roleId}";
    }
}
