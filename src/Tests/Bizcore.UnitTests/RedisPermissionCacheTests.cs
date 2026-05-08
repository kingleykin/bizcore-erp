using System.Text.Json;
using Bizcore.BuildingBlocks.Authorization;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;

namespace Bizcore.UnitTests
{
    public class RedisPermissionCacheTests
    {
        private readonly Mock<IConnectionMultiplexer> _redisMock;
        private readonly Mock<IDatabase> _dbMock;
        private readonly Mock<ILogger<RedisPermissionCache>> _loggerMock;
        private readonly RedisPermissionCache _cache;

        public RedisPermissionCacheTests()
        {
            _redisMock = new Mock<IConnectionMultiplexer>();
            _dbMock = new Mock<IDatabase>();
            _loggerMock = new Mock<ILogger<RedisPermissionCache>>();
            
            _redisMock.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(_dbMock.Object);
            
            _cache = new RedisPermissionCache(_redisMock.Object, _loggerMock.Object);
        }

        [Fact]
        public async Task GetAsync_ShouldReturnPermissions_WhenKeyExists()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var permissions = new[] { "Invoice.View", "Payment.Create" };
            var json = JsonSerializer.Serialize(permissions);
            
            _dbMock.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                   .ReturnsAsync((RedisValue)json);

            // Act
            var result = await _cache.GetAsync(userId);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(permissions);
        }

        [Fact]
        public async Task GetAsync_ShouldReturnNull_WhenKeyDoesNotExist()
        {
            // Arrange
            var userId = Guid.NewGuid();
            _dbMock.Setup(x => x.StringGetAsync(It.IsAny<RedisKey>(), It.IsAny<CommandFlags>()))
                   .ReturnsAsync(RedisValue.Null);

            // Act
            var result = await _cache.GetAsync(userId);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetAsync_ShouldCallRedisStringSet()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var permissions = new[] { "Invoice.View" };

            // Act
            await _cache.SetAsync(userId, permissions);

            // Assert
            _dbMock.Verify(x => x.StringSetAsync(
                It.Is<RedisKey>(k => k.ToString().Contains(userId.ToString())),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<bool>(),
                It.IsAny<When>(),
                It.IsAny<CommandFlags>()), Times.Once);
        }

        [Fact]
        public async Task InvalidateAsync_ShouldCallRedisKeyDelete()
        {
            // Arrange
            var userId = Guid.NewGuid();

            // Act
            await _cache.InvalidateAsync(userId);

            // Assert
            _dbMock.Verify(x => x.KeyDeleteAsync(
                It.Is<RedisKey>(k => k.ToString().Contains(userId.ToString())),
                It.IsAny<CommandFlags>()), Times.Once);
        }
    }
}
