using System.Security.Claims;
using Bizcore.BuildingBlocks.Authorization;
using FluentAssertions;
using Admin.API.Controllers;
using Admin.API.Domain.Entities;
using Admin.API.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace Bizcore.UnitTests
{
    public class MeControllerTests : IDisposable
    {
        private readonly AdminDbContext _db;
        private readonly Mock<IPermissionCache> _cacheMock;
        private readonly Mock<ILogger<MeController>> _loggerMock;
        private readonly MeController _controller;

        public MeControllerTests()
        {
            var options = new DbContextOptionsBuilder<AdminDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            
            _db = new AdminDbContext(options);
            _cacheMock = new Mock<IPermissionCache>();
            _loggerMock = new Mock<ILogger<MeController>>();
            
            _controller = new MeController(_db, _loggerMock.Object, _cacheMock.Object);
        }

        private void SetupUser(Guid userId, string username)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username)
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            
            _controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            };
        }

        [Fact]
        public async Task GetMyPermissions_ShouldReturnFromCache_WhenAvailable()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var username = "testuser";
            var cachedPermissions = new[] { "Invoice.View" };
            
            SetupUser(userId, username);
            _cacheMock.Setup(x => x.GetAsync(userId, It.IsAny<CancellationToken>()))
                      .ReturnsAsync(cachedPermissions);

            // Act
            var result = await _controller.GetMyPermissions(CancellationToken.None);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dto = okResult.Value.As<Admin.API.Application.DTOs.UserPermissionsDto>();
            dto.Permissions.Should().BeEquivalentTo(cachedPermissions);
            dto.UserId.Should().Be(userId);
        }

        [Fact]
        public async Task GetMyPermissions_ShouldLoadFromDbAndCache_WhenCacheMiss()
        {
            // Arrange
            var userId = Guid.NewGuid();
            var username = "dbuser";
            SetupUser(userId, username);
            
            // Seed DB
            var user = User.Create(username, "user@test.com", "hash");
            // Set private Id via reflection or use a public setter if available. 
            // In the actual entity it's private set. Use Reflection to force it for test.
            typeof(User).GetProperty("Id")!.SetValue(user, userId);
            
            var role = Role.Create("Admin", "Admin role");
            var permission = Permission.Create("Invoice.Create", "Create Invoice", "Invoice", "Action");
            
            _db.Users.Add(user);
            _db.Roles.Add(role);
            _db.Permissions.Add(permission);
            _db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
            _db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            await _db.SaveChangesAsync();

            _cacheMock.Setup(x => x.GetAsync(userId, It.IsAny<CancellationToken>()))
                      .ReturnsAsync((string[]?)null);

            // Act
            var result = await _controller.GetMyPermissions(CancellationToken.None);

            // Assert
            var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
            var dto = okResult.Value.As<Admin.API.Application.DTOs.UserPermissionsDto>();
            dto.Permissions.Should().Contain("Invoice.Create");
            
            _cacheMock.Verify(x => x.SetAsync(userId, It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        public void Dispose()
        {
            _db.Database.EnsureDeleted();
            _db.Dispose();
        }
    }
}
