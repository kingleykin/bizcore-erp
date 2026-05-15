using Asp.Versioning;
using Admin.API.Application.DTOs;
using Admin.API.Application.Commands;
using Admin.API.Application.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Bizcore.BuildingBlocks.Authorization;
using Bizcore.BuildingBlocks;

namespace Admin.API.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IMediator _mediator;

        public UsersController(IMediator mediator)
        {
            _mediator = mediator;
        }

    /// <summary>Lấy danh sách tất cả người dùng.</summary>
    [HttpGet]
    [RequirePermission(Permissions.Identity.Users.View)]
    public async Task<IActionResult> GetAll()
    {
        var users = await _mediator.Send(new GetUsersQuery());
        return Ok(users);
    }

    /// <summary>Lấy chi tiết một người dùng theo ID.</summary>
    [HttpGet("{id:guid}")]
    [RequirePermission(Permissions.Identity.Users.View)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var user = await _mediator.Send(new GetUserByIdQuery(id));
        return user == null ? NotFound() : Ok(user);
    }

    /// <summary>Tạo người dùng mới.</summary>
    [HttpPost]
    [RequirePermission(Permissions.Identity.Users.Create)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        var command = new CreateUserCommand(request);
        var user = await _mediator.Send(command);
        return CreatedAtAction(nameof(GetById), new { id = user.Id, version = "1.0" }, user);
    }

    /// <summary>Cập nhật thông tin người dùng.</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(Permissions.Identity.Users.Update)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request)
    {
        var command = new UpdateUserCommand(id, request);
        var user = await _mediator.Send(command);
        return Ok(user);
    }

    /// <summary>Vô hiệu hóa (soft-delete) người dùng.</summary>
    [HttpDelete("{id:guid}")]
    [RequirePermission(Permissions.Identity.Users.Delete)]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _mediator.Send(new DeleteUserCommand(id));
        return NoContent();
    }

    /// <summary>Gán (thay thế) danh sách role cho người dùng.</summary>
    [HttpPut("{id:guid}/roles")]
    [RequirePermission(Permissions.Identity.Users.ManageRoles)]
    public async Task<IActionResult> AssignRoles(Guid id, [FromBody] AssignRolesRequest request)
    {
        await _mediator.Send(new AssignRolesCommand(id, request));
        return NoContent();
    }

    /// <summary>Mở khóa tài khoản bị khóa do đăng nhập sai nhiều lần.</summary>
    [HttpPost("{id:guid}/unlock")]
    [RequirePermission(Permissions.Identity.Users.Update)]
    public async Task<IActionResult> Unlock(Guid id)
    {
        await _mediator.Send(new UnlockUserCommand(id));
        return NoContent();
    }

    /// <summary>Cập nhật ảnh đại diện của người dùng.</summary>
    [HttpPut("{id:guid}/avatar")]
    [RequirePermission(Permissions.Identity.Users.Update)]
    public async Task<IActionResult> UpdateAvatar(Guid id, [FromBody] string? avatarUrl)
    {
        await _mediator.Send(new UpdateAvatarCommand(id, avatarUrl));
        return NoContent();
    }

    /// <summary>Cập nhật ngôn ngữ ưu tiên của người dùng.</summary>
    [HttpPut("{id:guid}/language")]
    [Authorize] 
    public async Task<IActionResult> UpdateLanguage(Guid id, [FromBody] string languageCode)
    {
        await _mediator.Send(new UpdatePreferredLanguageCommand(id, languageCode));
        return NoContent();
    }
    }
}
