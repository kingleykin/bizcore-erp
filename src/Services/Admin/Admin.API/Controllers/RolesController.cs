using Asp.Versioning;
using Bizcore.BuildingBlocks;
using Bizcore.BuildingBlocks.Authorization;
using Admin.API.Application.DTOs;
using Admin.API.Application.Commands;
using Admin.API.Application.Queries;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;

namespace Admin.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/roles")]
[Authorize]
public class RolesController : ControllerBase
{
    private readonly IMediator _mediator;

    public RolesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Lấy danh sách tất cả roles.</summary>
    [HttpGet]
    [RequirePermission(Permissions.Identity.Roles.View)]
    public async Task<IActionResult> GetAll()
    {
        var roles = await _mediator.Send(new GetRolesQuery());
        return Ok(roles);
    }

    /// <summary>Lấy chi tiết một role theo ID.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Identity.Roles.View)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var role = await _mediator.Send(new GetRoleByIdQuery(id));
        return role == null ? NotFound() : Ok(role);
    }

    /// <summary>Tạo role mới.</summary>
    [HttpPost]
    [RequirePermission(Permissions.Identity.Roles.Create)]
    public async Task<IActionResult> Create([FromBody] CreateRoleRequest request)
    {
        var role = await _mediator.Send(new CreateRoleCommand(request));
        return CreatedAtAction(nameof(GetById), new { version = "1.0", id = role.Id }, role);
    }

    /// <summary>Cập nhật role.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Identity.Roles.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRoleRequest request)
    {
        var role = await _mediator.Send(new UpdateRoleCommand(id, request));
        return Ok(role);
    }

    /// <summary>Xóa role.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Identity.Roles.Delete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteRoleCommand(id));
        return NoContent();
    }

    /// <summary>Gán (thay thế) danh sách permission cho role.</summary>
    [HttpPut("{id:guid}/permissions")]
    [RequirePermission(Permissions.Identity.Roles.ManagePermissions)]
    public async Task<IActionResult> AssignPermissions(Guid id, [FromBody] AssignPermissionsRequest request)
    {
        await _mediator.Send(new AssignRolePermissionsCommand(id, request));
        return NoContent();
    }

    /// <summary>Lấy danh sách tất cả permissions có trong hệ thống.</summary>
    [HttpGet("permissions")]
    [RequirePermission(Permissions.Identity.Roles.View)]
    public async Task<IActionResult> GetAllPermissions()
    {
        var permissions = await _mediator.Send(new GetPermissionsQuery());
        return Ok(permissions);
    }
}
