using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Admin.API.Controllers;
using Admin.API.Application.DTOs;
using Admin.API.Application.Queries;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using Moq;
using Xunit;

namespace Bizcore.UnitTests;

public class MeControllerTests
{
    private readonly Mock<IMediator> _mediatorMock;
    private readonly MeController _controller;

    public MeControllerTests()
    {
        _mediatorMock = new Mock<IMediator>();
        _controller = new MeController(_mediatorMock.Object);
    }

    private void SetupUser(Guid userId, string username)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim("sub", userId.ToString())
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
    }

    [Fact]
    public async Task GetMyPermissions_ShouldReturnFromMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var username = "testuser";
        SetupUser(userId, username);

        var expectedDto = new UserPermissionsDto(userId, username, new[] { "Admin" }, new[] { "Invoice.View" });
        
        _mediatorMock.Setup(x => x.Send(It.IsAny<GetMyPermissionsQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.GetMyPermissions(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.As<UserPermissionsDto>();
        dto.Permissions.Should().Contain("Invoice.View");
        dto.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task GetMyNavigation_ShouldReturnFromMediator()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var username = "testuser";
        SetupUser(userId, username);

        var expectedDto = new NavigationMenuDto[] 
        { 
            new NavigationMenuDto(Guid.NewGuid(), null, "Dashboard", "/dashboard", "dashboard", 1) 
        };
        
        _mediatorMock.Setup(x => x.Send(It.IsAny<GetMyNavigationQuery>(), It.IsAny<CancellationToken>()))
                     .ReturnsAsync(expectedDto);

        // Act
        var result = await _controller.GetMyNavigation(CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.As<NavigationMenuDto[]>();
        dto.Should().HaveCount(1);
        dto[0].Name.Should().Be("Dashboard");
    }
}
