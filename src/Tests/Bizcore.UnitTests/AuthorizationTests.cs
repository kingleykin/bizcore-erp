using System.Security.Claims;
using Bizcore.BuildingBlocks.Authorization;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Bizcore.UnitTests
{
    public class AuthorizationTests
    {
        [Fact]
        public async Task DynamicPolicyProvider_ShouldCreatePolicyForUnknownName()
        {
            // Arrange
            var options = Options.Create(new AuthorizationOptions());
            var provider = new DynamicAuthorizationPolicyProvider(options);
            var policyName = "Invoice.View";

            // Act
            var policy = await provider.GetPolicyAsync(policyName);

            // Assert
            policy.Should().NotBeNull();
            policy!.Requirements.Should().ContainSingle(r => r is PermissionRequirement && ((PermissionRequirement)r).Permission == policyName);
        }

        [Fact]
        public async Task PermissionAuthorizationHandler_ShouldSucceed_WhenUserHasPermissionClaim()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PermissionAuthorizationHandler>>();
            var handler = new PermissionAuthorizationHandler(loggerMock.Object);
            
            var permission = "Invoice.Create";
            var requirement = new PermissionRequirement(permission);
            
            var claims = new List<Claim>
            {
                new Claim("permission", permission),
                new Claim(ClaimTypes.Name, "testuser")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            
            var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            context.HasSucceeded.Should().BeTrue();
        }

        [Fact]
        public async Task PermissionAuthorizationHandler_ShouldFail_WhenUserMissingPermissionClaim()
        {
            // Arrange
            var loggerMock = new Mock<ILogger<PermissionAuthorizationHandler>>();
            var handler = new PermissionAuthorizationHandler(loggerMock.Object);
            
            var permission = "Invoice.Delete";
            var requirement = new PermissionRequirement(permission);
            
            var claims = new List<Claim>
            {
                new Claim("permission", "Invoice.View"), // Different permission
                new Claim(ClaimTypes.Name, "testuser")
            };
            var identity = new ClaimsIdentity(claims, "TestAuth");
            var principal = new ClaimsPrincipal(identity);
            
            var context = new AuthorizationHandlerContext(new[] { requirement }, principal, null);

            // Act
            await handler.HandleAsync(context);

            // Assert
            context.HasSucceeded.Should().BeFalse();
        }
    }
}
